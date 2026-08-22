using ProjectHive.Core.Contracts;
using ProjectHive.Core.Events;
using System;
using System.Collections.Generic;
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
        private Transform customViewModelMagazine;
        private Transform customViewModelSlide;
        private bool usingCustomMagazineRig;
        private bool usingCustomRevolverRig;
        private Transform[] customRevolverRounds;
        private Transform[] customRevolverEjectedRounds;
        private Vector3 customRevolverCylinderBaseLocalPosition;
        private Quaternion customRevolverCylinderBaseLocalRotation = Quaternion.identity;
        private Vector3 customMagazineSocketLocalPosition;
        private Quaternion customMagazineSocketLocalRotation = Quaternion.identity;
        private Vector3 customSlideBaseLocalPosition;
        private Quaternion customSlideBaseLocalRotation = Quaternion.identity;
        private Vector3 viewModelBaseLocalPosition;
        private Quaternion viewModelBaseLocalRotation = Quaternion.identity;
        private Material runtimeVisualMaterial;
        private Material runtimeHandMaterial;
        private Material runtimeMagazineMaterial;
        private Material runtimeCartridgeMaterial;
        private FirearmAmmoState[] loadoutAmmoStates;
        private bool viewModelVisible = true;
        private const float CustomRevolverTurnStartSeconds = 0.10f;
        private const float CustomRevolverTurnEndSeconds = 0.39f;
        private const float CustomRevolverCylinderOpenStartSeconds = 0.39f;
        private const float CustomRevolverCylinderOpenEndSeconds = 0.49f;
        private const float CustomRevolverInsertionStartSeconds = 0.52f;

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
                return usingCustomMagazineRig ? reloadDuration * 1.35f : reloadDuration;

            int roundsToAnimate = Mathf.Max(1, reloadInsertedRoundCount);
            float duration = revolverReloadSecondsPerRound * roundsToAnimate;
            return usingCustomRevolverRig ? Mathf.Max(1.75f, duration * 1.45f) : duration;
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

            if (ballisticEffects != null)
                ballisticEffects.SetGunshotClip(definition.GunshotClip);
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
            generatedRevolverCylinder = null;

            if (definition != null && definition.ViewModelPrefab != null)
            {
                generatedVisualRoot = Instantiate(definition.ViewModelPrefab);
                generatedVisualRoot.name = $"{definition.DisplayName} View Model";
                generatedVisualRoot.transform.SetParent(root, false);
                viewModelBaseLocalPosition = definition.ViewModelLocalPosition;
                viewModelBaseLocalRotation = Quaternion.Euler(definition.ViewModelLocalEulerAngles);
                generatedVisualRoot.transform.localPosition = viewModelBaseLocalPosition;
                generatedVisualRoot.transform.localRotation = viewModelBaseLocalRotation;
                generatedVisualRoot.transform.localScale = definition.ViewModelLocalScale;
                SetupCustomPistolViewModel(generatedVisualRoot.transform);
                generatedVisualRoot.SetActive(viewModelVisible);
                return;
            }

            generatedVisualRoot = new GameObject("Generated Firearm View Model");
            generatedVisualRoot.transform.SetParent(root, false);
            viewModelBaseLocalPosition = Vector3.zero;
            viewModelBaseLocalRotation = Quaternion.identity;
            generatedVisualRoot.transform.localPosition = viewModelBaseLocalPosition;
            generatedVisualRoot.transform.localRotation = viewModelBaseLocalRotation;
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
            if (revolverReload && usingCustomRevolverRig)
            {
                float reloadElapsed = Time.time - reloadStartTime;
                float turnover = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(CustomRevolverTurnStartSeconds, CustomRevolverTurnEndSeconds, reloadElapsed));
                reloadPitch -= 360f * turnover;
            }

            generatedVisualRoot.transform.localPosition = viewModelBaseLocalPosition + new Vector3(
                reloadX,
                recoilLift * recoil + reloadY,
                -recoilDistance * recoil);
            generatedVisualRoot.transform.localRotation = viewModelBaseLocalRotation * Quaternion.Euler(
                -recoilAngle * recoil + reloadPitch,
                reloadYaw,
                reloadRoll);

            if (generatedRevolverCylinder != null)
            {
                float cylinderSpin = revolverReload
                    ? usingCustomRevolverRig
                        ? CalculateCustomRevolverCylinderSpin(Time.time - reloadStartTime)
                        : 72f * Mathf.Max(1, reloadInsertedRoundCount) * GetRevolverInsertionProgress(reloadProgress)
                    : 0f;
                if (usingCustomRevolverRig)
                {
                    float cylinderOpen = revolverReload
                        ? CalculateCustomRevolverCylinderOpen(reloadProgress, Time.time - reloadStartTime)
                        : 0f;
                    generatedRevolverCylinder.localPosition =
                        customRevolverCylinderBaseLocalPosition +
                        new Vector3(-0.026f * cylinderOpen, 0.003f * cylinderOpen, 0.005f * cylinderOpen);
                    generatedRevolverCylinder.localRotation =
                        customRevolverCylinderBaseLocalRotation * Quaternion.Euler(0f, cylinderSpin, 0f);
                }
                else
                {
                    generatedRevolverCylinder.localRotation = Quaternion.Euler(90f, cylinderSpin, 0f);
                }
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
            generatedLeftHandRoot.gameObject.SetActive(reloading && !usingCustomRevolverRig);
            if (!reloading)
            {
                if (customViewModelMagazine != null)
                    customViewModelMagazine.gameObject.SetActive(false);
                ResetCustomSlidePose();
                if (usingCustomRevolverRig)
                {
                    UpdateCustomRevolverAmmoVisuals(1f, false);
                    HideCustomRevolverEjectedRounds();
                }
                if (generatedReloadProp != null)
                    generatedReloadProp.gameObject.SetActive(false);
                if (generatedDroppedMagazine != null)
                    generatedDroppedMagazine.gameObject.SetActive(false);
                return;
            }

            if (FeedType == FirearmFeedType.Revolver)
                AnimateRevolverReloadHand(reload, reloadProgress);
            else
                AnimatePistolReloadHand(reloadProgress);
        }

        private void AnimatePistolReloadHand(float reloadProgress)
        {
            if (usingCustomMagazineRig)
            {
                AnimateCustomPistolMagazineReload(reloadProgress);
                return;
            }

            Vector3 magwell = new Vector3(0f, -0.22f, -0.12f);
            Vector3 heldMagazineOffset = new Vector3(0f, 0.08f, 0.02f);
            float insert = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.88f, reloadProgress));
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.88f, 1f, reloadProgress));
            bool magazineSeated = reloadProgress < 0.06f || reloadProgress >= 0.88f;
            Vector3 start = magwell - heldMagazineOffset + new Vector3(-0.025f, -0.28f, 0.015f);
            Vector3 socket = magwell - heldMagazineOffset;
            Vector3 stow = new Vector3(-0.27f, -0.27f, -0.12f);
            generatedLeftHandRoot.localPosition = Vector3.Lerp(Vector3.Lerp(start, socket, insert), stow, settle);
            generatedLeftHandRoot.localRotation = Quaternion.Euler(10f - 4f * insert, 0f, 6f - 12f * settle);

            if (customViewModelMagazine != null)
                customViewModelMagazine.gameObject.SetActive(magazineSeated);

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

        private void AnimateCustomPistolMagazineReload(float reloadProgress)
        {
            Vector3 socket = customMagazineSocketLocalPosition;
            Vector3 removedStart = socket + new Vector3(0f, 0.015f, 0f);
            Vector3 removedMid = socket + new Vector3(0.012f, -0.34f, -0.035f);
            Vector3 removedEnd = socket + new Vector3(0.06f, -0.9f, 0.08f);
            Vector3 insertStart = socket + new Vector3(-0.018f, -0.55f, -0.015f);
            Vector3 insertGuide = socket + new Vector3(-0.006f, -0.17f, -0.004f);
            Vector3 handOffset = new Vector3(-0.055f, -0.055f, -0.02f);
            float drop = Mathf.Clamp01(Mathf.InverseLerp(0f, 0.62f, reloadProgress));
            float dropFall = Mathf.SmoothStep(0f, 1f, drop);
            float approach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.72f, reloadProgress));
            float insert = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.96f, reloadProgress));
            float stow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.90f, 1f, reloadProgress));

            if (customViewModelMagazine != null)
            {
                customViewModelMagazine.gameObject.SetActive(false);
                customViewModelMagazine.localPosition = socket;
                customViewModelMagazine.localRotation = customMagazineSocketLocalRotation;
            }
            AnimateCustomSlidePull(reloadProgress);

            if (generatedDroppedMagazine != null)
            {
                bool dropping = reloadProgress < 0.62f;
                generatedDroppedMagazine.gameObject.SetActive(dropping);
                if (dropping)
                {
                    generatedDroppedMagazine.localPosition =
                        Vector3.Lerp(
                            Vector3.Lerp(removedStart, removedMid, dropFall),
                            Vector3.Lerp(removedMid, removedEnd, dropFall),
                            dropFall);
                    generatedDroppedMagazine.localRotation =
                        customMagazineSocketLocalRotation * Quaternion.Euler(35f * dropFall, 16f * dropFall, -240f * dropFall);
                }
            }

            if (generatedReloadProp != null)
            {
                bool inserting = reloadProgress >= 0.42f && reloadProgress < 0.985f;
                generatedReloadProp.gameObject.SetActive(inserting);
                if (inserting)
                {
                    Vector3 reloadPosition = Vector3.Lerp(
                        Vector3.Lerp(insertStart, insertGuide, approach),
                        socket,
                        insert);
                    generatedReloadProp.localPosition = reloadPosition;
                    generatedReloadProp.localRotation =
                        customMagazineSocketLocalRotation * Quaternion.Euler(0f, 0f, 4f * (1f - insert));

                    if (generatedLeftHandRoot != null)
                    {
                        generatedLeftHandRoot.localPosition = Vector3.Lerp(
                            reloadPosition + handOffset,
                            new Vector3(-0.32f, -0.22f, -0.12f),
                            stow);
                        generatedLeftHandRoot.localRotation = Quaternion.Euler(8f, 0f, 8f - 18f * insert);
                    }
                }
            }

            AnimateCustomSlideHand(reloadProgress);
        }

        private void AnimateCustomSlidePull(float reloadProgress)
        {
            if (customViewModelSlide == null)
                return;

            const float slidePullDistance = 0.063f;
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.96f, reloadProgress));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.96f, 1f, reloadProgress));
            float slide = pull * (1f - release);
            customViewModelSlide.localPosition =
                customSlideBaseLocalPosition + new Vector3(0f, 0f, -slidePullDistance * slide);
            customViewModelSlide.localRotation =
                customSlideBaseLocalRotation * Quaternion.Euler(-4f * slide, 0f, 0f);
        }

        private void AnimateCustomSlideHand(float reloadProgress)
        {
            if (generatedLeftHandRoot == null)
                return;

            const float slidePullDistance = 0.063f;
            float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.86f, reloadProgress));
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.96f, reloadProgress));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.96f, 1f, reloadProgress));
            float slide = pull * (1f - release);
            float active = reach * (1f - release);
            if (active <= 0f)
                return;

            generatedLeftHandRoot.gameObject.SetActive(true);
            Vector3 ready = generatedLeftHandRoot.localPosition;
            Vector3 grab = new Vector3(-0.035f, 0.075f, 0.045f);
            Vector3 pulled = grab + new Vector3(0f, 0f, -slidePullDistance * slide);
            Vector3 releasePose = new Vector3(-0.30f, -0.20f, -0.12f);
            generatedLeftHandRoot.localPosition =
                Vector3.Lerp(Vector3.Lerp(ready, pulled, reach), releasePose, release);
            generatedLeftHandRoot.localRotation = Quaternion.Euler(
                -18f + 8f * release,
                0f,
                -24f + 12f * release);
        }

        private void ResetCustomSlidePose()
        {
            if (customViewModelSlide == null)
                return;

            customViewModelSlide.localPosition = customSlideBaseLocalPosition;
            customViewModelSlide.localRotation = customSlideBaseLocalRotation;
        }

        private void AnimateRevolverReloadHand(float reload, float reloadProgress)
        {
            if (usingCustomRevolverRig)
            {
                AnimateCustomRevolverReloadHand(reloadProgress);
                return;
            }

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
                generatedReloadProp.localRotation = usingCustomRevolverRig
                    ? Quaternion.Euler(0f, 180f, 180f)
                    : Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private void AnimateCustomRevolverReloadHand(float reloadProgress)
        {
            if (generatedLeftHandRoot != null)
                generatedLeftHandRoot.gameObject.SetActive(false);

            int roundsToAnimate = Mathf.Max(1, reloadInsertedRoundCount);
            float reloadElapsed = Time.time - reloadStartTime;
            float rawStep = GetCustomRevolverInsertionRawStep(reloadElapsed);
            float cappedStep = Mathf.Min(rawStep, roundsToAnimate);
            float insertionProgress = Mathf.Clamp01(cappedStep / roundsToAnimate);
            float chamberStep = cappedStep >= roundsToAnimate ? 1f : Mathf.Repeat(cappedStep, 1f);
            float approach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.28f, chamberStep));
            float insert = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.62f, chamberStep));
            float withdraw = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.64f, 0.84f, chamberStep));
            float finish = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f, 1f, reloadProgress));

            int visibleInsertedRounds = Mathf.Clamp(Mathf.FloorToInt(cappedStep), 0, roundsToAnimate);
            if (chamberStep >= 0.62f && visibleInsertedRounds < roundsToAnimate)
                visibleInsertedRounds++;

            UpdateCustomRevolverAmmoVisuals((float)visibleInsertedRounds / roundsToAnimate, true);
            AnimateCustomRevolverEjection(reloadElapsed);

            Vector3 ready = new Vector3(-0.18f, -0.105f, 0.09f);
            Vector3 chamberMouth = new Vector3(-0.115f, -0.095f, 0.105f);
            Vector3 seated = new Vector3(-0.078f, -0.095f, 0.105f);
            Vector3 stow = new Vector3(-0.34f, -0.17f, -0.10f);
            Vector3 cartridgePosition =
                Vector3.Lerp(
                    Vector3.Lerp(ready, chamberMouth, approach),
                    seated,
                    insert);

            if (generatedReloadProp != null)
            {
                bool showCartridge =
                    reloadElapsed >= CustomRevolverInsertionStartSeconds &&
                    insertionProgress > 0f &&
                    reloadProgress < 0.98f &&
                    chamberStep >= 0.08f &&
                    chamberStep < 0.66f;
                generatedReloadProp.gameObject.SetActive(showCartridge);
                if (showCartridge)
                {
                    generatedReloadProp.localPosition = cartridgePosition;
                    generatedReloadProp.localRotation = Quaternion.Euler(0f, 180f, 180f);
                }
            }
        }

        private void AnimateCustomRevolverEjection(float reloadElapsed)
        {
            if (customRevolverEjectedRounds == null || customRevolverEjectedRounds.Length == 0)
                return;

            bool ejecting = reloadElapsed >= 0.62f && reloadElapsed < 1.12f;
            float drop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.12f, reloadElapsed));
            for (int i = 0; i < customRevolverEjectedRounds.Length; i++)
            {
                Transform round = customRevolverEjectedRounds[i];
                if (round == null)
                    continue;

                round.gameObject.SetActive(ejecting);
                if (!ejecting)
                    continue;

                float side = i - (customRevolverEjectedRounds.Length - 1) * 0.5f;
                Vector3 start = new Vector3(-0.035f + side * 0.008f, -0.095f, 0.09f + side * 0.006f);
                Vector3 arc = start + new Vector3(-0.03f + side * 0.012f, -0.18f, 0.025f);
                Vector3 end = start + new Vector3(-0.07f + side * 0.018f, -0.72f, -0.035f);
                round.localPosition =
                    Vector3.Lerp(
                        Vector3.Lerp(start, arc, drop),
                        Vector3.Lerp(arc, end, drop),
                        drop);
                round.localRotation = Quaternion.Euler(
                    60f * drop,
                    180f + 460f * drop + side * 20f,
                    180f + 720f * drop);
            }
        }

        private void HideCustomRevolverEjectedRounds()
        {
            if (customRevolverEjectedRounds == null)
                return;

            for (int i = 0; i < customRevolverEjectedRounds.Length; i++)
            {
                if (customRevolverEjectedRounds[i] != null)
                    customRevolverEjectedRounds[i].gameObject.SetActive(false);
            }
        }

        private static float GetRevolverInsertionProgress(float reloadProgress)
        {
            return Mathf.Clamp01(reloadProgress / 0.82f);
        }

        private static float GetCustomRevolverInsertionProgress(float reloadProgress)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.96f, reloadProgress));
        }

        private static float CalculateCustomRevolverCylinderOpen(float reloadProgress, float reloadElapsed)
        {
            float open = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    CustomRevolverCylinderOpenStartSeconds,
                    CustomRevolverCylinderOpenEndSeconds,
                    reloadElapsed));
            float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.92f, 1f, reloadProgress));
            return open * (1f - close);
        }

        private float CalculateCustomRevolverCylinderSpin(float reloadElapsed)
        {
            int roundsToAnimate = Mathf.Max(1, reloadInsertedRoundCount);
            float cappedStep = Mathf.Min(GetCustomRevolverInsertionRawStep(reloadElapsed), roundsToAnimate);
            int chamberIndex = Mathf.Clamp(Mathf.FloorToInt(cappedStep), 0, roundsToAnimate - 1);
            float chamberStep = cappedStep >= roundsToAnimate ? 1f : Mathf.Repeat(cappedStep, 1f);
            float advance = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.98f, chamberStep));
            return 60f * (chamberIndex + advance);
        }

        private float GetCustomRevolverInsertionRawStep(float reloadElapsed)
        {
            float secondsPerRound = Mathf.Max(0.05f, revolverReloadSecondsPerRound);
            return Mathf.Max(0f, (reloadElapsed - CustomRevolverInsertionStartSeconds) / secondsPerRound);
        }

        private static float CalculateRevolverReloadPose(float reloadProgress)
        {
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.16f, reloadProgress));
            float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.88f, 1f, reloadProgress));
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
            customViewModelMagazine = null;
            customViewModelSlide = null;
            usingCustomMagazineRig = false;
            usingCustomRevolverRig = false;
            customRevolverRounds = null;
            customRevolverEjectedRounds = null;
            customRevolverCylinderBaseLocalPosition = Vector3.zero;
            customRevolverCylinderBaseLocalRotation = Quaternion.identity;
            customMagazineSocketLocalPosition = Vector3.zero;
            customMagazineSocketLocalRotation = Quaternion.identity;
            customSlideBaseLocalPosition = Vector3.zero;
            customSlideBaseLocalRotation = Quaternion.identity;
        }

        private void SetupCustomPistolViewModel(Transform root)
        {
            usingCustomMagazineRig = false;
            usingCustomRevolverRig = false;

            if (FeedType == FirearmFeedType.Revolver)
            {
                SetupCustomRevolverViewModel(root);
                return;
            }

            customViewModelMagazine = FindChildByNamePart(root, "Magazine");
            customViewModelSlide = FindChildByNamePart(root, "Body");
            TrySplitCustomBodyForSlide();
            if (customViewModelSlide != null)
            {
                customSlideBaseLocalPosition = customViewModelSlide.localPosition;
                customSlideBaseLocalRotation = customViewModelSlide.localRotation;
            }
            Transform looseBullet = FindChildByNamePart(root, "Bullet");
            if (looseBullet != null)
                looseBullet.gameObject.SetActive(false);
            BuildCustomPistolReloadRig(root);
        }

        private void SetupCustomRevolverViewModel(Transform root)
        {
            generatedRevolverCylinder = FindChildByNamePart(root, "Cylinder");
            if (generatedRevolverCylinder == null)
                return;

            usingCustomRevolverRig = true;
            customRevolverCylinderBaseLocalPosition = generatedRevolverCylinder.localPosition;
            customRevolverCylinderBaseLocalRotation = generatedRevolverCylinder.localRotation;
            customRevolverRounds = CollectCustomRevolverRounds(generatedRevolverCylinder);
            UpdateCustomRevolverAmmoVisuals(1f, false);
            BuildCustomRevolverReloadRig(root);
        }

        private Transform[] CollectCustomRevolverRounds(Transform cylinder)
        {
            List<Transform> rounds = new List<Transform>();
            for (int i = 0; i < cylinder.childCount; i++)
            {
                Transform child = cylinder.GetChild(i);
                if (child.name.IndexOf("Shell", StringComparison.OrdinalIgnoreCase) >= 0)
                    rounds.Add(child);
            }

            return rounds.ToArray();
        }

        private void BuildCustomRevolverReloadRig(Transform root)
        {
            GameObject hand = new GameObject("Left Reload Hand");
            hand.transform.SetParent(root, false);
            generatedLeftHandRoot = hand.transform;

            if (customRevolverRounds != null && customRevolverRounds.Length > 0 && customRevolverRounds[0] != null)
            {
                GameObject cartridge = Instantiate(customRevolverRounds[0].gameObject, root, false);
                cartridge.name = "Reload Reichsrevolver Cartridge";
                generatedReloadProp = cartridge.transform;
                generatedReloadProp.localPosition = new Vector3(-0.32f, -0.08f, -0.03f);
                generatedReloadProp.localRotation = Quaternion.Euler(0f, 180f, 180f);
                generatedReloadProp.localScale = Vector3.one;
                generatedReloadProp.gameObject.SetActive(false);

                customRevolverEjectedRounds = new Transform[customRevolverRounds.Length];
                for (int i = 0; i < customRevolverRounds.Length; i++)
                {
                    GameObject ejected = Instantiate(customRevolverRounds[i].gameObject, root, false);
                    ejected.name = $"Ejected Reichsrevolver Cartridge {i + 1}";
                    customRevolverEjectedRounds[i] = ejected.transform;
                    ejected.SetActive(false);
                }
            }

            hand.SetActive(false);
        }

        private void UpdateCustomRevolverAmmoVisuals(float insertionProgress, bool reloading)
        {
            if (customRevolverRounds == null || customRevolverRounds.Length == 0)
                return;

            int liveRounds = CountRevolverRounds(RevolverChamberState.Live);
            int visibleRounds = reloading
                ? Mathf.Clamp(Mathf.FloorToInt(insertionProgress * Mathf.Max(1, reloadInsertedRoundCount)), 0, liveRounds)
                : liveRounds;

            for (int i = 0; i < customRevolverRounds.Length; i++)
            {
                Transform round = customRevolverRounds[i];
                if (round == null)
                    continue;

                round.gameObject.SetActive(i < visibleRounds);
            }
        }

        private void BuildCustomPistolReloadRig(Transform root)
        {
            if (customViewModelMagazine == null)
                return;

            GameObject hand = new GameObject("Left Reload Hand");
            hand.transform.SetParent(root, false);
            generatedLeftHandRoot = hand.transform;

            usingCustomMagazineRig = true;
            customMagazineSocketLocalPosition = new Vector3(0f, -0.09f, -0.075f);
            customMagazineSocketLocalRotation = Quaternion.identity;
            customViewModelMagazine.localPosition = customMagazineSocketLocalPosition;
            customViewModelMagazine.localRotation = customMagazineSocketLocalRotation;
            customViewModelMagazine.gameObject.SetActive(false);

            GameObject reloadMagazine = Instantiate(customViewModelMagazine.gameObject, root, false);
            reloadMagazine.name = "Reload Magazine";
            generatedReloadProp = reloadMagazine.transform;
            generatedReloadProp.localPosition = customMagazineSocketLocalPosition;
            generatedReloadProp.localRotation = customMagazineSocketLocalRotation;
            generatedReloadProp.gameObject.SetActive(false);

            GameObject droppedMagazine = Instantiate(customViewModelMagazine.gameObject, root, false);
            droppedMagazine.name = "Dropped Magazine";
            generatedDroppedMagazine = droppedMagazine.transform;
            generatedDroppedMagazine.localPosition = customMagazineSocketLocalPosition;
            generatedDroppedMagazine.localRotation = customMagazineSocketLocalRotation;
            generatedDroppedMagazine.gameObject.SetActive(false);

            hand.SetActive(false);
        }

        private void TrySplitCustomBodyForSlide()
        {
            if (customViewModelSlide == null)
                return;

            MeshFilter sourceFilter = customViewModelSlide.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = customViewModelSlide.GetComponent<MeshRenderer>();
            if (sourceFilter == null || sourceRenderer == null || sourceFilter.sharedMesh == null)
                return;

            Mesh sourceMesh = sourceFilter.sharedMesh;
            float slideThreshold = sourceMesh.bounds.center.y + sourceMesh.bounds.extents.y * 0.55f;
            Mesh slideMesh = CreateMeshPart(sourceMesh, slideThreshold, true);
            Mesh frameMesh = CreateMeshPart(sourceMesh, slideThreshold, false);
            if (slideMesh == null || frameMesh == null)
                return;

            GameObject frame = new GameObject("Generated FiveSeven Frame");
            frame.transform.SetParent(customViewModelSlide.parent, false);
            frame.transform.localPosition = customViewModelSlide.localPosition;
            frame.transform.localRotation = customViewModelSlide.localRotation;
            frame.transform.localScale = customViewModelSlide.localScale;
            MeshFilter frameFilter = frame.AddComponent<MeshFilter>();
            frameFilter.sharedMesh = frameMesh;
            MeshRenderer frameRenderer = frame.AddComponent<MeshRenderer>();
            frameRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            GameObject slide = new GameObject("Generated FiveSeven Slide");
            slide.transform.SetParent(customViewModelSlide.parent, false);
            slide.transform.localPosition = customViewModelSlide.localPosition;
            slide.transform.localRotation = customViewModelSlide.localRotation;
            slide.transform.localScale = customViewModelSlide.localScale;
            MeshFilter slideFilter = slide.AddComponent<MeshFilter>();
            slideFilter.sharedMesh = slideMesh;
            MeshRenderer slideRenderer = slide.AddComponent<MeshRenderer>();
            slideRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            sourceRenderer.enabled = false;
            customViewModelSlide = slide.transform;
        }

        private static Mesh CreateMeshPart(Mesh sourceMesh, float localYThreshold, bool upper)
        {
            Vector3[] vertices = sourceMesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return null;

            Mesh mesh = new Mesh();
            mesh.name = upper ? $"{sourceMesh.name}_SlidePart" : $"{sourceMesh.name}_FramePart";
            mesh.vertices = vertices;
            mesh.normals = sourceMesh.normals;
            mesh.tangents = sourceMesh.tangents;
            mesh.uv = sourceMesh.uv;
            mesh.uv2 = sourceMesh.uv2;
            mesh.colors = sourceMesh.colors;
            mesh.bindposes = sourceMesh.bindposes;
            mesh.subMeshCount = sourceMesh.subMeshCount;

            int triangleCount = 0;
            for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
            {
                int[] sourceTriangles = sourceMesh.GetTriangles(subMesh);
                List<int> triangles = new List<int>(sourceTriangles.Length);
                for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
                {
                    int a = sourceTriangles[i];
                    int b = sourceTriangles[i + 1];
                    int c = sourceTriangles[i + 2];
                    float centerY = (vertices[a].y + vertices[b].y + vertices[c].y) / 3f;
                    if (upper == centerY >= localYThreshold)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                    }
                }

                triangleCount += triangles.Count;
                mesh.SetTriangles(triangles, subMesh);
            }

            if (triangleCount == 0)
            {
                Destroy(mesh);
                return null;
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Transform FindChildByNamePart(Transform root, string namePart)
        {
            if (root == null || string.IsNullOrWhiteSpace(namePart))
                return null;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != root &&
                    child.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }

            return null;
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
