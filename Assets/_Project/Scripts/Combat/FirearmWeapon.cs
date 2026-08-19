using ProjectHive.Core.Contracts;
using ProjectHive.Core.Events;
using System;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class FirearmWeapon : MonoBehaviour, IWeaponStateProvider
    {
        [Header("Definition")]
        [SerializeField] private FirearmDefinition definition;
        [SerializeField] private FirearmDefinition[] loadoutDefinitions;
        [SerializeField] private int equippedLoadoutIndex;
        [SerializeField] private string fallbackWeaponItemId = "weapon.firearm.prototype";
        [SerializeField] private FirearmFeedType fallbackFeedType = FirearmFeedType.DetachableMagazine;

        [Header("Shot")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private FirearmBallisticEffects ballisticEffects;
        [SerializeField] private float damage = 25f;
        [SerializeField, Min(1f)] private float defaultHeadshotMultiplier = 2f;
        [SerializeField] private float fireRate = 6f;
        [SerializeField] private float maxDistance = 120f;
        [SerializeField, Min(0f)] private float fullDamageDistance = 25f;
        [SerializeField, Range(0f, 1f)] private float minimumDamageMultiplier = 0.35f;
        [SerializeField] private float spreadDegrees = 0.75f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float gunshotLoudness = 1f;
        [SerializeField] private float gunshotRadius = 55f;

        [Header("Ammunition")]
        [SerializeField, Min(0)] private int magazineCapacity = 10;
        [SerializeField, Min(1)] private int cylinderCapacity = 5;
        [SerializeField, Min(0)] private int reserveRounds = 30;
        [SerializeField] private bool startLoaded = true;
        [SerializeField] private RevolverChamberState[] revolverChambers = new RevolverChamberState[5];

        [Header("View Model")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Material visualMaterial;
        [SerializeField, Min(0.05f)] private float reloadDuration = 0.85f;
        [SerializeField, Min(0.05f)] private float revolverReloadSecondsPerRound = 0.55f;
        [SerializeField, Min(0.01f)] private float recoilDuration = 0.14f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.08f;
        [SerializeField, Min(0f)] private float recoilLift = 0.035f;
        [SerializeField, Min(0f)] private float recoilAngle = 8f;
        [SerializeField] private Color leftHandColor = new Color(0.82f, 0.58f, 0.42f, 1f);
        [SerializeField] private Color magazineColor = new Color(0.08f, 0.09f, 0.1f, 1f);
        [SerializeField] private Color cartridgeColor = new Color(0.95f, 0.72f, 0.26f, 1f);

        private float nextFireTime;
        private float reloadStartTime;
        private float reloadEndTime;
        private float activeReloadDuration;
        private float recoilEndTime;
        private int magazineRounds;
        private int chamberedRounds;
        private int activeCylinderIndex;
        private int reloadInsertedRoundCount;
        private WeaponActionState actionState = WeaponActionState.Ready;
        private GameObject generatedVisualRoot;
        private Transform generatedRevolverCylinder;
        private Transform generatedLeftHandRoot;
        private Transform generatedReloadProp;
        private Transform generatedDroppedMagazine;
        private Material runtimeVisualMaterial;
        private Material runtimeHandMaterial;
        private Material runtimeMagazineMaterial;
        private Material runtimeCartridgeMaterial;
        private FirearmAmmoState[] loadoutAmmoStates;
        private bool viewModelVisible = true;

        public bool CanFire => Time.time >= nextFireTime;
        public int EquippedLoadoutIndex => equippedLoadoutIndex;
        public FirearmDefinition CurrentDefinition => definition;
        public string DisplayName => definition != null ? definition.DisplayName : fallbackWeaponItemId;
        public string AmmoText => CreateAmmoText();
        public bool IsReloading => actionState == WeaponActionState.Reloading && Time.time < reloadEndTime;
        public bool IsRevolver => FeedType == FirearmFeedType.Revolver;
        public int ActiveCylinderIndex => activeCylinderIndex;
        public WeaponStateSnapshot WeaponState => CreateSnapshot();
        public event Action<WeaponStateSnapshot> WeaponStateChanged;

        private FirearmFeedType FeedType =>
            definition != null ? definition.FeedType : fallbackFeedType;

        private void Awake()
        {
            if (ballisticEffects == null)
                ballisticEffects = GetComponent<FirearmBallisticEffects>();

            EquipLoadoutIndex(Mathf.Max(0, equippedLoadoutIndex), true);
            ApplyDefinition();
            InitializeAmmo();
            RebuildViewModel();
            PublishState();
        }

        private void Update()
        {
            if (actionState == WeaponActionState.Reloading && !IsReloading)
            {
                actionState = WeaponActionState.Ready;
                PublishState();
            }

            AnimateViewModel();
        }

        public bool TryEquipLoadoutSlot(int slotNumber)
        {
            return EquipLoadoutIndex(slotNumber - 1, false);
        }

        public void SetViewModelVisible(bool visible)
        {
            viewModelVisible = visible;
            if (generatedVisualRoot != null)
                generatedVisualRoot.SetActive(visible);
        }

        public bool EquipLoadoutIndex(int index, bool force)
        {
            if (loadoutDefinitions == null || loadoutDefinitions.Length == 0)
                return false;

            if (index < 0 || index >= loadoutDefinitions.Length)
                return false;

            FirearmDefinition nextDefinition = loadoutDefinitions[index];
            if (nextDefinition == null)
                return false;

            if (!force && equippedLoadoutIndex == index && definition == nextDefinition)
                return false;

            if (!force)
                SaveCurrentAmmoState();

            equippedLoadoutIndex = index;
            definition = nextDefinition;
            ApplyDefinition();
            RestoreOrResetAmmoState(index);
            RebuildViewModel();
            PublishState();
            return true;
        }

        public bool TryFire(GameObject owner, Vector3 origin, Vector3 forward, out RaycastHit hit)
        {
            hit = default;
            if (!CanFire || IsReloading)
                return false;

            if (!TryConsumeRound())
            {
                nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
                PublishState();
                return false;
            }

            actionState = WeaponActionState.Attacking;
            nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
            recoilEndTime = Time.time + recoilDuration;
            Vector3 shotOrigin = origin;
            Vector3 shotDirection = ApplySpread(forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward);

            EmitGunshot(owner, muzzle != null ? muzzle.position : shotOrigin);

            if (!TryHitscanHit(owner, shotOrigin, shotDirection, out hit))
            {
                PlayShotEffects(owner, shotOrigin, shotDirection, false, default, false);
                actionState = WeaponActionState.Ready;
                PublishState();
                return true;
            }

            bool hitDamageable = false;
            if (CombatHitUtility.TryGetDamageable(hit.collider, out IDamageable damageable, out GameObject target) &&
                !CombatHitUtility.IsSelfHit(owner, target))
            {
                hitDamageable = true;
                DamageHitZone hitZone = ResolveHitZone(hit.collider, out float damageMultiplier);
                DamageFlags flags =
                    hitZone == DamageHitZone.Head || hitZone == DamageHitZone.WeakPoint
                        ? DamageFlags.Critical
                        : DamageFlags.None;
                float distanceMultiplier = CalculateDistanceDamageMultiplier(hit.distance);
                DamageData damageData = new DamageData(
                    damage * damageMultiplier * distanceMultiplier,
                    DamageKind.Projectile,
                    hitZone,
                    flags,
                    hit.point,
                    shotDirection,
                    owner);
                damageable.ApplyDamage(in damageData);
            }

            PlayShotEffects(owner, shotOrigin, shotDirection, true, hit, hitDamageable);
            actionState = WeaponActionState.Ready;
            PublishState();
            return true;
        }

        public bool TryReload()
        {
            if (reserveRounds <= 0 || IsReloading)
                return false;

            bool changed = FeedType == FirearmFeedType.Revolver
                ? ReloadRevolver()
                : ReloadMagazine();

            if (changed)
            {
                actionState = WeaponActionState.Reloading;
                reloadStartTime = Time.time;
                activeReloadDuration = CalculateReloadDuration();
                reloadEndTime = reloadStartTime + activeReloadDuration;
                PublishState();
            }

            return changed;
        }

        public RevolverChamberState[] GetRevolverChamberStates()
        {
            return CloneChambers();
        }

        private bool TryConsumeRound()
        {
            if (FeedType == FirearmFeedType.Revolver)
                return TryConsumeRevolverRound();

            if (chamberedRounds <= 0)
                return false;

            chamberedRounds = 0;
            if (magazineRounds > 0)
            {
                magazineRounds--;
                chamberedRounds = 1;
            }

            return true;
        }

        private bool TryConsumeRevolverRound()
        {
            EnsureRevolverChambers();
            if (revolverChambers[activeCylinderIndex] != RevolverChamberState.Live)
            {
                AdvanceCylinder();
                return false;
            }

            revolverChambers[activeCylinderIndex] = RevolverChamberState.Spent;
            AdvanceCylinder();
            return true;
        }

        private bool ReloadMagazine()
        {
            reloadInsertedRoundCount = 0;
            int roundsNeeded = Mathf.Max(0, magazineCapacity - magazineRounds);
            int roundsToLoad = Mathf.Min(roundsNeeded, reserveRounds);

            if (roundsToLoad <= 0 && chamberedRounds > 0)
                return false;

            magazineRounds += roundsToLoad;
            reserveRounds -= roundsToLoad;

            if (chamberedRounds <= 0 && magazineRounds > 0)
            {
                magazineRounds--;
                chamberedRounds = 1;
            }

            return roundsToLoad > 0;
        }

        private bool ReloadRevolver()
        {
            EnsureRevolverChambers();
            bool changed = false;
            int loadedRounds = 0;

            for (int i = 0; i < revolverChambers.Length; i++)
            {
                if (revolverChambers[i] == RevolverChamberState.Spent)
                {
                    revolverChambers[i] = RevolverChamberState.Empty;
                    changed = true;
                }
            }

            for (int i = 0; i < revolverChambers.Length && reserveRounds > 0; i++)
            {
                if (revolverChambers[i] != RevolverChamberState.Empty)
                    continue;

                revolverChambers[i] = RevolverChamberState.Live;
                reserveRounds--;
                loadedRounds++;
                changed = true;
            }

            reloadInsertedRoundCount = loadedRounds;
            return changed;
        }

        private float CalculateReloadDuration()
        {
            if (FeedType != FirearmFeedType.Revolver)
                return reloadDuration;

            int roundsToAnimate = Mathf.Max(1, reloadInsertedRoundCount);
            return revolverReloadSecondsPerRound * roundsToAnimate;
        }

        private bool TryHitscanHit(GameObject owner, Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            if (!Physics.Raycast(origin, direction, out hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
                return false;

            return owner == null || !hit.transform.IsChildOf(owner.transform);
        }

        private void PlayShotEffects(
            GameObject owner,
            Vector3 shotOrigin,
            Vector3 shotDirection,
            bool hasHit,
            RaycastHit hit,
            bool hitDamageable)
        {
            if (ballisticEffects == null)
                return;

            Vector3 muzzlePosition = muzzle != null ? muzzle.position : shotOrigin;
            FirearmShotEffectContext context = new FirearmShotEffectContext(
                owner,
                shotOrigin,
                muzzlePosition,
                shotDirection,
                maxDistance,
                hasHit,
                hasHit ? hit.point : Vector3.zero,
                hasHit ? hit.normal : -shotDirection,
                hasHit ? hit.collider : null,
                hitDamageable);
            ballisticEffects.PlayShot(in context);
        }

        private Vector3 ApplySpread(Vector3 forward)
        {
            if (spreadDegrees <= 0f)
                return forward;

            Vector2 random = UnityEngine.Random.insideUnitCircle * spreadDegrees;
            Quaternion rotation = Quaternion.Euler(random.y, random.x, 0f);
            return rotation * forward;
        }

        private void EmitGunshot(GameObject owner, Vector3 position)
        {
            GameEventBus bus = GameEventBus.Instance;
            if (bus == null)
                return;

            NoiseEvent noise = new NoiseEvent(
                position,
                gunshotLoudness,
                gunshotRadius,
                NoiseCategory.Gunshot,
                NoiseAffiliation.Player,
                owner != null ? owner.GetInstanceID() : gameObject.GetInstanceID(),
                Time.time);
            bus.PublishNoise(in noise);
        }

        private DamageHitZone ResolveHitZone(Collider hitCollider, out float damageMultiplier)
        {
            damageMultiplier = 1f;
            if (hitCollider == null)
                return DamageHitZone.Body;

            DamageHitZoneMarker marker = hitCollider.GetComponentInParent<DamageHitZoneMarker>();
            if (marker != null)
            {
                damageMultiplier = Mathf.Max(0f, marker.DamageMultiplier);
                return marker.HitZone;
            }

            if (hitCollider.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                damageMultiplier = defaultHeadshotMultiplier;
                return DamageHitZone.Head;
            }

            return DamageHitZone.Body;
        }

        private float CalculateDistanceDamageMultiplier(float hitDistance)
        {
            if (maxDistance <= fullDamageDistance)
                return 1f;

            float normalized = Mathf.InverseLerp(fullDamageDistance, maxDistance, hitDistance);
            return Mathf.Lerp(1f, minimumDamageMultiplier, normalized);
        }

        private void ApplyDefinition()
        {
            if (definition == null)
                return;

            damage = definition.Damage;
            fireRate = definition.FireRate;
            maxDistance = definition.MaxDistance;
            fullDamageDistance = definition.FullDamageDistance;
            minimumDamageMultiplier = definition.MinimumDamageMultiplier;
            spreadDegrees = definition.SpreadDegrees;
            magazineCapacity = definition.MagazineCapacity;
            cylinderCapacity = definition.CylinderCapacity;
            gunshotLoudness = definition.GunshotLoudness;
            gunshotRadius = definition.GunshotRadius;
        }

        private void InitializeAmmo()
        {
            if (magazineRounds > 0 ||
                chamberedRounds > 0 ||
                CountRevolverRounds(RevolverChamberState.Live) > 0)
            {
                return;
            }

            ResetAmmo();
        }

        private void ResetAmmo()
        {
            magazineRounds = 0;
            chamberedRounds = 0;
            activeCylinderIndex = 0;
            EnsureRevolverChambers();

            for (int i = 0; i < revolverChambers.Length; i++)
                revolverChambers[i] = RevolverChamberState.Empty;

            if (FeedType == FirearmFeedType.Revolver)
            {
                if (!startLoaded)
                    return;

                for (int i = 0; i < revolverChambers.Length; i++)
                    revolverChambers[i] = RevolverChamberState.Live;
                return;
            }

            if (!startLoaded)
                return;

            magazineRounds = magazineCapacity;
            chamberedRounds = magazineCapacity > 0 ? 1 : 0;
        }

        private void SaveCurrentAmmoState()
        {
            EnsureAmmoStateCapacity();
            if (loadoutAmmoStates == null ||
                equippedLoadoutIndex < 0 ||
                equippedLoadoutIndex >= loadoutAmmoStates.Length)
            {
                return;
            }

            loadoutAmmoStates[equippedLoadoutIndex] = new FirearmAmmoState(
                true,
                magazineRounds,
                chamberedRounds,
                reserveRounds,
                activeCylinderIndex,
                CloneChambers());
        }

        private void RestoreOrResetAmmoState(int index)
        {
            EnsureAmmoStateCapacity();
            if (loadoutAmmoStates != null &&
                index >= 0 &&
                index < loadoutAmmoStates.Length &&
                loadoutAmmoStates[index].HasValue)
            {
                FirearmAmmoState state = loadoutAmmoStates[index];
                magazineRounds = state.MagazineRounds;
                chamberedRounds = state.ChamberedRounds;
                reserveRounds = state.ReserveRounds;
                activeCylinderIndex = state.ActiveCylinderIndex;
                revolverChambers = state.Chambers != null
                    ? (RevolverChamberState[])state.Chambers.Clone()
                    : null;
                EnsureRevolverChambers();
                return;
            }

            ResetAmmo();
        }

        private void EnsureAmmoStateCapacity()
        {
            int capacity = loadoutDefinitions != null ? loadoutDefinitions.Length : 0;
            if (capacity <= 0)
                return;

            if (loadoutAmmoStates != null && loadoutAmmoStates.Length == capacity)
                return;

            FirearmAmmoState[] previous = loadoutAmmoStates;
            loadoutAmmoStates = new FirearmAmmoState[capacity];

            if (previous == null)
                return;

            int copyCount = Mathf.Min(previous.Length, loadoutAmmoStates.Length);
            for (int i = 0; i < copyCount; i++)
                loadoutAmmoStates[i] = previous[i];
        }

        private RevolverChamberState[] CloneChambers()
        {
            EnsureRevolverChambers();
            return (RevolverChamberState[])revolverChambers.Clone();
        }

        private void RebuildViewModel()
        {
            Transform root = visualRoot != null ? visualRoot : transform;
            ClearGeneratedViewModel();

            generatedVisualRoot = new GameObject("Generated Firearm View Model");
            generatedVisualRoot.transform.SetParent(root, false);
            generatedVisualRoot.transform.localPosition = Vector3.zero;
            generatedVisualRoot.transform.localRotation = Quaternion.identity;
            generatedRevolverCylinder = null;

            FirearmVisualProfile profile =
                definition != null
                    ? definition.VisualProfile
                    : FirearmVisualProfile.Pistol;
            generatedVisualRoot.transform.localScale =
                profile == FirearmVisualProfile.Revolver
                    ? Vector3.one * 0.55f
                    : Vector3.one * 0.52f;
            Color color =
                definition != null
                    ? definition.DisplayColor
                    : new Color(0.12f, 0.14f, 0.16f, 1f);

            Shader viewShader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            runtimeVisualMaterial =
                visualMaterial != null
                    ? new Material(visualMaterial)
                    : new Material(viewShader);
            runtimeVisualMaterial.color = color;
            runtimeHandMaterial = new Material(viewShader);
            runtimeHandMaterial.color = leftHandColor;
            runtimeMagazineMaterial = new Material(viewShader);
            runtimeMagazineMaterial.color = magazineColor;
            runtimeCartridgeMaterial = new Material(viewShader);
            runtimeCartridgeMaterial.color = cartridgeColor;

            if (profile == FirearmVisualProfile.Revolver)
            {
                BuildRevolverViewModel(generatedVisualRoot.transform);
                BuildReloadHand(generatedVisualRoot.transform, true);
            }
            else
            {
                BuildPistolViewModel(generatedVisualRoot.transform);
                BuildReloadHand(generatedVisualRoot.transform, false);
            }

            generatedVisualRoot.SetActive(viewModelVisible);
        }

        private void AnimateViewModel()
        {
            if (generatedVisualRoot == null)
                return;

            float recoil = 0f;
            if (recoilEndTime > Time.time && recoilDuration > 0f)
            {
                float normalized = Mathf.Clamp01((recoilEndTime - Time.time) / recoilDuration);
                recoil = Mathf.Sin(normalized * Mathf.PI);
            }

            float reload = 0f;
            float reloadProgress = 0f;
            if (IsReloading && activeReloadDuration > 0f)
            {
                reloadProgress = Mathf.InverseLerp(reloadStartTime, reloadEndTime, Time.time);
                reload = Mathf.Sin(reloadProgress * Mathf.PI);
            }

            bool revolverReload = FeedType == FirearmFeedType.Revolver && IsReloading;
            bool pistolReload = FeedType != FirearmFeedType.Revolver && IsReloading;
            float revolverPose = revolverReload ? CalculateRevolverReloadPose(reloadProgress) : reload;
            float pistolPose = pistolReload ? CalculatePistolReloadPose(reloadProgress) : reload;
            float reloadPitch = revolverReload ? 46f * revolverPose : -18f * pistolPose;
            float reloadYaw = revolverReload ? 18f * reload : 18f * pistolPose;
            float reloadRoll = revolverReload ? -36f * revolverPose : -26f * pistolPose;
            float reloadX = pistolReload ? 0.07f * pistolPose : 0f;
            float reloadY = revolverReload ? -0.13f * revolverPose : 0.07f * pistolPose;

            generatedVisualRoot.transform.localPosition = new Vector3(
                reloadX,
                recoilLift * recoil + reloadY,
                -recoilDistance * recoil);
            generatedVisualRoot.transform.localRotation = Quaternion.Euler(
                -recoilAngle * recoil + reloadPitch,
                reloadYaw,
                reloadRoll);

            if (generatedRevolverCylinder != null)
            {
                float cylinderSpin = revolverReload
                    ? 72f * Mathf.Max(1, reloadInsertedRoundCount) * GetRevolverInsertionProgress(reloadProgress)
                    : 0f;
                generatedRevolverCylinder.localRotation = Quaternion.Euler(90f, cylinderSpin, 0f);
            }

            AnimateReloadHand(reload, reloadProgress);
        }

        private void BuildPistolViewModel(Transform root)
        {
            AddPart(root, "Pistol Slide", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0.1f, 0.4f));
            AddPart(root, "Pistol Grip", PrimitiveType.Cube, new Vector3(0f, -0.15f, -0.12f), new Vector3(0.16f, 0.28f, 0.16f), Quaternion.Euler(-18f, 0f, 0f));
            AddPart(root, "Pistol Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0.24f), new Vector3(0.055f, 0.14f, 0.055f), Quaternion.Euler(90f, 0f, 0f));
        }

        private void BuildRevolverViewModel(Transform root)
        {
            AddPart(root, "Revolver Frame", PrimitiveType.Cube, new Vector3(0f, 0f, -0.02f), new Vector3(0.24f, 0.16f, 0.32f));
            generatedRevolverCylinder = AddPart(root, "Revolver Cylinder", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.04f), new Vector3(0.2f, 0.14f, 0.2f), Quaternion.Euler(90f, 0f, 0f)).transform;
            AddPart(root, "Revolver Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0.28f), new Vector3(0.065f, 0.24f, 0.065f), Quaternion.Euler(90f, 0f, 0f));
            AddPart(root, "Revolver Grip", PrimitiveType.Cube, new Vector3(0f, -0.18f, -0.14f), new Vector3(0.17f, 0.31f, 0.17f), Quaternion.Euler(-24f, 0f, 0f));
        }

        private void BuildReloadHand(Transform root, bool revolver)
        {
            GameObject hand = new GameObject("Left Reload Hand");
            hand.transform.SetParent(root, false);
            generatedLeftHandRoot = hand.transform;

            AddPart(hand.transform, "Left Palm", PrimitiveType.Cube, Vector3.zero, new Vector3(0.16f, 0.08f, 0.12f), null, runtimeHandMaterial);
            AddPart(hand.transform, "Left Thumb", PrimitiveType.Cube, new Vector3(0.08f, 0.02f, 0.01f), new Vector3(0.035f, 0.04f, 0.1f), Quaternion.Euler(0f, 0f, -28f), runtimeHandMaterial);
            AddPart(hand.transform, "Left Fingers", PrimitiveType.Cube, new Vector3(-0.02f, 0.045f, 0.03f), new Vector3(0.13f, 0.035f, 0.08f), Quaternion.Euler(18f, 0f, 0f), runtimeHandMaterial);

            if (revolver)
            {
                generatedReloadProp = AddPart(
                    hand.transform,
                    "Reload Cartridge",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.03f, 0.1f),
                    new Vector3(0.025f, 0.13f, 0.025f),
                    Quaternion.Euler(90f, 0f, 0f),
                    runtimeCartridgeMaterial).transform;
            }
            else
            {
                generatedReloadProp = AddPart(
                    hand.transform,
                    "Reload Magazine",
                    PrimitiveType.Cube,
                    new Vector3(0f, 0.08f, 0.02f),
                    new Vector3(0.09f, 0.18f, 0.06f),
                    Quaternion.identity,
                    runtimeMagazineMaterial).transform;
                generatedDroppedMagazine = AddPart(
                    root,
                    "Dropped Magazine",
                    PrimitiveType.Cube,
                    new Vector3(0f, -0.22f, -0.12f),
                    new Vector3(0.085f, 0.17f, 0.055f),
                    Quaternion.identity,
                    runtimeMagazineMaterial).transform;
                generatedDroppedMagazine.gameObject.SetActive(false);
            }

            hand.SetActive(false);
        }

        private GameObject AddPart(
            Transform root,
            string partName,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion? localRotation = null,
            Material overrideMaterial = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(root, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            part.transform.localScale = localScale;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null)
            {
                partCollider.enabled = false;
                Destroy(partCollider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = overrideMaterial != null ? overrideMaterial : runtimeVisualMaterial;

            return part;
        }

        private void AnimateReloadHand(float reload, float reloadProgress)
        {
            if (generatedLeftHandRoot == null)
                return;

            bool reloading = IsReloading;
            generatedLeftHandRoot.gameObject.SetActive(reloading);
            if (!reloading)
                return;

            if (FeedType == FirearmFeedType.Revolver)
                AnimateRevolverReloadHand(reload, reloadProgress);
            else
                AnimatePistolReloadHand(reloadProgress);
        }

        private void AnimatePistolReloadHand(float reloadProgress)
        {
            Vector3 magwell = new Vector3(0f, -0.22f, -0.12f);
            Vector3 heldMagazineOffset = new Vector3(0f, 0.08f, 0.02f);
            float insert = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.88f, reloadProgress));
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.88f, 1f, reloadProgress));
            Vector3 start = magwell - heldMagazineOffset + new Vector3(-0.025f, -0.28f, 0.015f);
            Vector3 socket = magwell - heldMagazineOffset;
            Vector3 stow = new Vector3(-0.27f, -0.27f, -0.12f);
            generatedLeftHandRoot.localPosition = Vector3.Lerp(Vector3.Lerp(start, socket, insert), stow, settle);
            generatedLeftHandRoot.localRotation = Quaternion.Euler(10f - 4f * insert, 0f, 6f - 12f * settle);

            if (generatedReloadProp != null)
            {
                generatedReloadProp.gameObject.SetActive(reloadProgress >= 0.28f && reloadProgress < 0.96f);
                generatedReloadProp.localPosition = heldMagazineOffset;
                generatedReloadProp.localRotation = Quaternion.identity;
            }

            if (generatedDroppedMagazine != null)
            {
                bool dropping = reloadProgress < 0.42f;
                generatedDroppedMagazine.gameObject.SetActive(dropping);
                if (dropping)
                {
                    float drop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.42f, reloadProgress));
                    generatedDroppedMagazine.localPosition = new Vector3(
                        magwell.x,
                        magwell.y - 0.46f * drop,
                        magwell.z + 0.03f * drop);
                    generatedDroppedMagazine.localRotation = Quaternion.Euler(0f, 0f, 170f * drop);
                }
            }
        }

        private void AnimateRevolverReloadHand(float reload, float reloadProgress)
        {
            int roundsToAnimate = Mathf.Max(1, reloadInsertedRoundCount);
            float insertionProgress = GetRevolverInsertionProgress(reloadProgress);
            float chamberStep = Mathf.Repeat(insertionProgress * roundsToAnimate, 1f);
            float insert = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.72f, chamberStep));
            float reset = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, chamberStep));
            float push = insert * (1f - reset);
            float finish = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, reloadProgress));

            Vector3 ready = new Vector3(-0.3f, -0.05f, 0.0f);
            Vector3 chamber = new Vector3(-0.11f, 0.0f, 0.05f);
            Vector3 stow = new Vector3(-0.34f, -0.16f, -0.08f);
            generatedLeftHandRoot.localPosition = Vector3.Lerp(
                Vector3.Lerp(ready, chamber, 0.55f + 0.45f * push),
                stow,
                finish);
            generatedLeftHandRoot.localRotation = Quaternion.Euler(
                8f - 8f * finish,
                22f + 5f * push,
                30f - 22f * finish);

            if (generatedReloadProp != null)
            {
                generatedReloadProp.gameObject.SetActive(reloadProgress < 0.82f);
                generatedReloadProp.localPosition = new Vector3(0.02f, 0.035f, 0.13f - 0.08f * push);
                generatedReloadProp.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static float GetRevolverInsertionProgress(float reloadProgress)
        {
            return Mathf.Clamp01(reloadProgress / 0.82f);
        }

        private static float CalculateRevolverReloadPose(float reloadProgress)
        {
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.12f, reloadProgress));
            float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, reloadProgress));
            return open * (1f - close);
        }

        private static float CalculatePistolReloadPose(float reloadProgress)
        {
            float raise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, reloadProgress));
            float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, reloadProgress));
            return raise * (1f - lower);
        }

        private void ClearGeneratedViewModel()
        {
            if (generatedVisualRoot == null)
                return;

            Destroy(generatedVisualRoot);
            generatedVisualRoot = null;
            generatedRevolverCylinder = null;
            generatedLeftHandRoot = null;
            generatedReloadProp = null;
            generatedDroppedMagazine = null;
        }

        private void AdvanceCylinder()
        {
            activeCylinderIndex =
                revolverChambers.Length == 0
                    ? 0
                    : (activeCylinderIndex + 1) % revolverChambers.Length;
        }

        private void EnsureRevolverChambers()
        {
            int capacity = Mathf.Max(1, cylinderCapacity);
            if (revolverChambers != null && revolverChambers.Length == capacity)
                return;

            RevolverChamberState[] previous = revolverChambers;
            revolverChambers = new RevolverChamberState[capacity];

            if (previous == null)
                return;

            int copyCount = Mathf.Min(previous.Length, revolverChambers.Length);
            for (int i = 0; i < copyCount; i++)
                revolverChambers[i] = previous[i];
        }

        private WeaponStateSnapshot CreateSnapshot()
        {
            int readyRounds = FeedType == FirearmFeedType.Revolver
                ? CountRevolverRounds(RevolverChamberState.Live)
                : magazineRounds;
            int capacity = FeedType == FirearmFeedType.Revolver
                ? cylinderCapacity
                : magazineCapacity;
            int chamberRounds = FeedType == FirearmFeedType.Revolver
                ? 0
                : chamberedRounds;

            return new WeaponStateSnapshot(
                definition != null ? definition.WeaponItemId : fallbackWeaponItemId,
                EquippedWeaponSlot.Firearm,
                actionState,
                FeedType,
                readyRounds,
                capacity,
                chamberRounds,
                reserveRounds);
        }

        private int CountRevolverRounds(RevolverChamberState state)
        {
            EnsureRevolverChambers();
            int count = 0;
            for (int i = 0; i < revolverChambers.Length; i++)
            {
                if (revolverChambers[i] == state)
                    count++;
            }

            return count;
        }

        private void PublishState()
        {
            WeaponStateChanged?.Invoke(WeaponState);
        }

        private string CreateAmmoText()
        {
            if (FeedType == FirearmFeedType.Revolver)
                return $"{CountRevolverRounds(RevolverChamberState.Live)}/{cylinderCapacity} | {reserveRounds}";

            int loadedRounds = magazineRounds + chamberedRounds;
            return $"{loadedRounds}/{magazineCapacity + 1} | {reserveRounds}";
        }

        private void OnValidate()
        {
            if (loadoutDefinitions != null &&
                loadoutDefinitions.Length > 0 &&
                equippedLoadoutIndex >= 0 &&
                equippedLoadoutIndex < loadoutDefinitions.Length &&
                loadoutDefinitions[equippedLoadoutIndex] != null)
            {
                definition = loadoutDefinitions[equippedLoadoutIndex];
            }

            ApplyDefinition();
            damage = Mathf.Max(0f, damage);
            defaultHeadshotMultiplier = Mathf.Max(1f, defaultHeadshotMultiplier);
            fireRate = Mathf.Max(0.01f, fireRate);
            maxDistance = Mathf.Max(1f, maxDistance);
            fullDamageDistance = Mathf.Clamp(fullDamageDistance, 0f, maxDistance);
            minimumDamageMultiplier = Mathf.Clamp01(minimumDamageMultiplier);
            spreadDegrees = Mathf.Max(0f, spreadDegrees);
            magazineCapacity = Mathf.Max(0, magazineCapacity);
            cylinderCapacity = Mathf.Max(1, cylinderCapacity);
            reserveRounds = Mathf.Max(0, reserveRounds);
            reloadDuration = Mathf.Max(0.05f, reloadDuration);
            revolverReloadSecondsPerRound = Mathf.Max(0.05f, revolverReloadSecondsPerRound);
            recoilDuration = Mathf.Max(0.01f, recoilDuration);
            recoilDistance = Mathf.Max(0f, recoilDistance);
            recoilLift = Mathf.Max(0f, recoilLift);
            recoilAngle = Mathf.Max(0f, recoilAngle);
            EnsureRevolverChambers();
        }

        private readonly struct FirearmAmmoState
        {
            public FirearmAmmoState(
                bool hasValue,
                int magazineRounds,
                int chamberedRounds,
                int reserveRounds,
                int activeCylinderIndex,
                RevolverChamberState[] chambers)
            {
                HasValue = hasValue;
                MagazineRounds = magazineRounds;
                ChamberedRounds = chamberedRounds;
                ReserveRounds = reserveRounds;
                ActiveCylinderIndex = activeCylinderIndex;
                Chambers = chambers;
            }

            public bool HasValue { get; }
            public int MagazineRounds { get; }
            public int ChamberedRounds { get; }
            public int ReserveRounds { get; }
            public int ActiveCylinderIndex { get; }
            public RevolverChamberState[] Chambers { get; }
        }
    }
}
