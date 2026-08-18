using ProjectHive.Data.Runs;
using UnityEngine;

namespace ProjectHive.Integration.VerticalSlice
{
    public enum PrototypeFrontEndScreen
    {
        Title = 0,
        MapSelection = 1,
        LoadoutConfirmation = 2,
        Shelter = 3,
        CharacterStorage = 4,
        Result = 5,
        Loading = 6
    }

    [DisallowMultipleComponent]
    public sealed class PrototypeFrontEndController : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private PrototypeGameSession session;
        private PrototypeFrontEndScreen currentScreen = PrototypeFrontEndScreen.Title;
        private RunResult displayedResult;
        private string displayedResultMessage = string.Empty;
        private string selectedMapId = PrototypeGameSession.DefaultMapId;
        private Vector2 stashScrollPosition;

        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle playButtonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle lockedButtonStyle;
        private GUIStyle slotStyle;
        private GUIStyle tabStyle;

        public PrototypeFrontEndScreen CurrentScreen => currentScreen;
        public string SelectedMapId => selectedMapId;

        private void Start()
        {
            session = PrototypeGameSession.Instance;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (session != null && session.TryConsumeResult(out RunResult result, out string message))
            {
                displayedResult = result;
                displayedResultMessage = message;
                currentScreen = result.Outcome == RunOutcome.Extracted
                    ? PrototypeFrontEndScreen.CharacterStorage
                    : PrototypeFrontEndScreen.Result;
            }
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OpenTitle() => currentScreen = PrototypeFrontEndScreen.Title;
        public void OpenMapSelection() => currentScreen = PrototypeFrontEndScreen.MapSelection;
        public void OpenShelter() => currentScreen = PrototypeFrontEndScreen.Shelter;
        public void OpenCharacterStorage() => currentScreen = PrototypeFrontEndScreen.CharacterStorage;

        public void SelectPrototypeMap()
        {
            selectedMapId = PrototypeGameSession.DefaultMapId;
        }

        public void OpenLoadoutConfirmation()
        {
            if (!string.IsNullOrWhiteSpace(selectedMapId))
                currentScreen = PrototypeFrontEndScreen.LoadoutConfirmation;
        }

        public bool BeginRaid()
        {
            if (session == null)
                session = PrototypeGameSession.Instance;
            if (session == null)
                return false;

            currentScreen = PrototypeFrontEndScreen.Loading;
            if (session.StartRaid(selectedMapId))
                return true;

            currentScreen = PrototypeFrontEndScreen.LoadoutConfirmation;
            return false;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawPhysicalBackground();

            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            Vector3 offset = new Vector3(
                (Screen.width - ReferenceWidth * scale) * 0.5f,
                (Screen.height - ReferenceHeight * scale) * 0.5f,
                0f);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawReferenceBackground();
            switch (currentScreen)
            {
                case PrototypeFrontEndScreen.MapSelection:
                    DrawMapSelection();
                    break;
                case PrototypeFrontEndScreen.LoadoutConfirmation:
                    DrawLoadoutConfirmation();
                    break;
                case PrototypeFrontEndScreen.Shelter:
                    DrawShelter();
                    break;
                case PrototypeFrontEndScreen.CharacterStorage:
                    DrawCharacterStorage();
                    break;
                case PrototypeFrontEndScreen.Result:
                    DrawResult();
                    break;
                case PrototypeFrontEndScreen.Loading:
                    DrawLoadingFallback();
                    break;
                default:
                    DrawTitle();
                    break;
            }

            GUI.matrix = previousMatrix;
        }

        private void DrawTitle()
        {
            GUI.Label(new Rect(110f, 92f, 950f, 100f), "PROJECT HIVE", titleStyle);
            GUI.Label(new Rect(116f, 185f, 720f, 36f), "SURFACE PROTOTYPE", smallStyle);

            Rect menu = new Rect(720f, 430f, 480f, 390f);
            DrawPanel(menu);
            if (GUI.Button(new Rect(784f, 470f, 352f, 70f), "PLAY", playButtonStyle))
                OpenMapSelection();
            if (MenuButton(new Rect(800f, 565f, 320f, 62f), "HIDEOUT"))
                OpenShelter();
            if (MenuButton(new Rect(800f, 645f, 320f, 62f), "CHARACTER"))
                OpenCharacterStorage();
            if (MenuButton(new Rect(800f, 725f, 320f, 62f), "EXIT"))
                QuitGame();
        }

        private void DrawMapSelection()
        {
            GUI.Label(new Rect(105f, 70f, 1100f, 72f), "MAP", titleStyle);

            bool selected = selectedMapId == PrototypeGameSession.DefaultMapId;
            if (GUI.Button(new Rect(470f, 395f, 310f, 78f), "●  도심 구역", selected ? selectedButtonStyle : buttonStyle))
                SelectPrototypeMap();

            GUI.enabled = false;
            GUI.Button(new Rect(960f, 255f, 310f, 78f), "▣  LOCKED", lockedButtonStyle);
            GUI.Button(new Rect(1190f, 580f, 310f, 78f), "▣  LOCKED", lockedButtonStyle);
            GUI.Button(new Rect(640f, 710f, 310f, 78f), "▣  LOCKED", lockedButtonStyle);
            GUI.enabled = true;

            if (MenuButton(new Rect(120f, 935f, 260f, 62f), "뒤로"))
                OpenTitle();
            if (MenuButton(new Rect(1530f, 935f, 270f, 62f), "START"))
                OpenLoadoutConfirmation();
        }

        private void DrawLoadoutConfirmation()
        {
            DrawTopBar("장비 확인", "현재 장비를 가지고 지상으로 이동한다");
            DrawPanel(new Rect(120f, 210f, 760f, 650f));
            DrawTab(new Rect(155f, 180f, 210f, 58f), "PRESET");
            DrawPresetContents(new Rect(165f, 270f, 670f, 560f), true);

            DrawPanel(new Rect(950f, 210f, 850f, 650f));
            GUI.Label(new Rect(1000f, 250f, 700f, 46f), "도심 구역", headingStyle);

            if (MenuButton(new Rect(120f, 935f, 260f, 62f), "뒤로"))
                OpenMapSelection();
            if (MenuButton(new Rect(1430f, 920f, 370f, 78f), "지상으로 이동"))
                BeginRaid();
        }

        private void DrawShelter()
        {
            GUI.Label(new Rect(105f, 70f, 1100f, 72f), "HIDEOUT", titleStyle);
            DrawPanel(new Rect(170f, 200f, 1580f, 610f));
            GUIStyle centered = new GUIStyle(headingStyle) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(170f, 200f, 1580f, 610f), "미구현", centered);

            if (MenuButton(new Rect(120f, 935f, 260f, 62f), "메인으로"))
                OpenTitle();
        }

        private void DrawCharacterStorage()
        {
            DrawPanel(new Rect(20f, 100f, 640f, 800f));
            DrawPanel(new Rect(680f, 100f, 1220f, 800f));
            DrawTab(new Rect(45f, 60f, 220f, 58f), "PRESET");
            DrawTab(new Rect(710f, 60f, 200f, 58f), "STASH");

            DrawPresetContents(new Rect(50f, 145f, 580f, 700f), false);

            Rect stashViewport = new Rect(720f, 145f, 1140f, 700f);
            Rect stashContent = new Rect(0f, 0f, 1040f, 1210f);
            stashScrollPosition = GUI.BeginScrollView(
                stashViewport,
                stashScrollPosition,
                stashContent,
                false,
                true);

            const int columns = 10;
            const int rows = 12;
            const float size = 88f;
            const float gap = 12f;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Rect slot = new Rect(10f + column * (size + gap), 10f + row * (size + gap), size, size);
                    GUI.Box(slot, string.Empty, slotStyle);
                }
            }
            GUI.EndScrollView();

            if (MenuButton(new Rect(785f, 950f, 350f, 62f), "MAIN"))
                OpenTitle();
        }

        private void DrawResult()
        {
            DrawTopBar("지상 활동 결과", "");
            DrawPanel(new Rect(450f, 270f, 1020f, 500f));

            bool success = displayedResult != null && displayedResult.Outcome == RunOutcome.Extracted;
            string resultTitle = success ? "생환" : "사망";
            GUI.Label(new Rect(560f, 340f, 800f, 90f), resultTitle, titleStyle);
            GUI.Label(new Rect(560f, 460f, 800f, 140f), displayedResultMessage, labelStyle);
            if (displayedResult != null)
            {
                GUI.Label(new Rect(560f, 600f, 800f, 80f),
                    $"지역: 도심 구역\n활동 시간: {displayedResult.DurationSeconds:0.0}초", smallStyle);
            }

            if (MenuButton(new Rect(785f, 820f, 350f, 72f), "확인"))
                OpenCharacterStorage();
        }

        private void DrawLoadingFallback()
        {
            GUI.Label(new Rect(510f, 450f, 900f, 80f), "지상으로 이동하는 중", headingStyle);
        }

        private void DrawTopBar(string title, string subtitle)
        {
            GUI.Label(new Rect(105f, 70f, 1100f, 72f), title, titleStyle);
            GUI.Label(new Rect(110f, 145f, 1200f, 38f), subtitle, smallStyle);
            DrawRect(new Rect(105f, 185f, 1710f, 2f), new Color(0.42f, 0.46f, 0.45f, 0.7f));
        }

        private void DrawPresetContents(Rect bounds, bool compact)
        {
            float gap = compact ? 10f : 12f;
            float weaponHeight = compact ? 58f : 68f;
            float columnWidth = (bounds.width - gap * 2f) / 3f;

            DrawEquipmentSlot(
                new Rect(bounds.x, bounds.y, bounds.width, weaponHeight),
                "1번 무기",
                "Prototype Pistol");
            DrawEquipmentSlot(
                new Rect(bounds.x, bounds.y + weaponHeight + gap, bounds.width, weaponHeight),
                "2번 무기",
                "—");

            float equipmentY = bounds.y + (weaponHeight + gap) * 2f;
            DrawEquipmentSlot(
                new Rect(bounds.x, equipmentY, columnWidth, columnWidth),
                "근접무기",
                "Knife");
            DrawEquipmentSlot(
                new Rect(bounds.x + columnWidth + gap, equipmentY, columnWidth, columnWidth),
                "투척물",
                "—");

            float stackedHeight = (columnWidth - gap) * 0.5f;
            float thirdColumnX = bounds.x + (columnWidth + gap) * 2f;
            DrawEquipmentSlot(
                new Rect(thirdColumnX, equipmentY, columnWidth, stackedHeight),
                "방어구",
                "Basic Armor");
            DrawEquipmentSlot(
                new Rect(thirdColumnX, equipmentY + stackedHeight + gap, columnWidth, stackedHeight),
                "가방",
                "Small Backpack");

            float pocketY = equipmentY + columnWidth + (compact ? 12f : 20f);
            GUI.Label(new Rect(bounds.x, pocketY, 220f, 34f), "안전 포켓", smallStyle);
            float pocketSize = compact ? 68f : 82f;
            GUI.Box(new Rect(bounds.x, pocketY + 36f, pocketSize, pocketSize), string.Empty, slotStyle);
            GUI.Box(new Rect(bounds.x + pocketSize + gap, pocketY + 36f, pocketSize, pocketSize), string.Empty, slotStyle);
        }

        private void DrawEquipmentSlot(Rect rect, string slotName, string itemName)
        {
            GUI.Box(rect, string.Empty, slotStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 28f, 30f), slotName, smallStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 39f, rect.width - 28f, rect.height - 44f), itemName, labelStyle);
        }

        private void DrawTab(Rect rect, string label)
        {
            DrawRect(rect, new Color(0.055f, 0.065f, 0.068f, 1f));
            DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(0.5f, 0.55f, 0.53f, 0.7f));
            GUI.Label(rect, label, tabStyle);
        }

        private bool MenuButton(Rect rect, string text)
        {
            return GUI.Button(rect, text, buttonStyle);
        }

        private static void DrawPanel(Rect rect)
        {
            DrawRect(rect, new Color(0.035f, 0.043f, 0.046f, 0.93f));
            DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(0.5f, 0.55f, 0.53f, 0.55f));
        }

        private void DrawPhysicalBackground()
        {
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawReferenceBackground()
        {
            DrawRect(new Rect(0f, 0f, ReferenceWidth, ReferenceHeight), new Color(0.012f, 0.017f, 0.019f, 1f));
            DrawRect(new Rect(0f, 0f, ReferenceWidth, 14f), new Color(0.32f, 0.36f, 0.35f, 0.8f));
            DrawRect(new Rect(0f, 900f, ReferenceWidth, 180f), new Color(0.02f, 0.028f, 0.03f, 0.9f));
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.9f, 0.92f, 0.9f, 1f) }
            };
            headingStyle = new GUIStyle(titleStyle)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.83f, 0.86f, 0.84f, 1f) }
            };
            smallStyle = new GUIStyle(labelStyle)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.58f, 0.63f, 0.61f, 1f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.87f, 0.89f, 0.87f, 1f) },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            playButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 27
            };
            selectedButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 26,
                normal = { textColor = new Color(0.72f, 0.95f, 0.8f, 1f) }
            };
            lockedButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { textColor = new Color(0.35f, 0.38f, 0.37f, 1f) }
            };
            slotStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { textColor = new Color(0.8f, 0.84f, 0.82f, 1f) }
            };
            tabStyle = new GUIStyle(headingStyle)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
