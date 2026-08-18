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

        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle lockedButtonStyle;
        private GUIStyle slotStyle;

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
                currentScreen = PrototypeFrontEndScreen.Result;
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

            Rect menu = new Rect(1320f, 380f, 420f, 350f);
            DrawPanel(menu);
            if (MenuButton(new Rect(1360f, 420f, 340f, 64f), "PLAY"))
                OpenMapSelection();
            if (MenuButton(new Rect(1360f, 500f, 340f, 64f), "은신처"))
                OpenShelter();
            if (MenuButton(new Rect(1360f, 580f, 340f, 64f), "캐릭터"))
                OpenCharacterStorage();
            if (MenuButton(new Rect(1360f, 660f, 340f, 64f), "나가기"))
                QuitGame();

            GUI.Label(new Rect(112f, 970f, 1000f, 32f), "23:00  /  지상 활동 준비", smallStyle);
        }

        private void DrawMapSelection()
        {
            DrawTopBar("지역 선택", "지상으로 이동할 지역을 선택한다");
            DrawMapLines();

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
            GUI.Label(new Rect(165f, 250f, 650f, 46f), "장착 장비", headingStyle);

            DrawLoadoutRow(165f, 335f, "총기", "Prototype Pistol");
            DrawLoadoutRow(165f, 420f, "근접무기", "Knife");
            DrawLoadoutRow(165f, 505f, "방어구", "Basic Armor");
            DrawLoadoutRow(165f, 590f, "가방", "Small Backpack");
            DrawLoadoutRow(165f, 675f, "안전 포켓", "2 Slots");
            DrawLoadoutRow(165f, 760f, "퀵슬롯", "Bandage");

            DrawPanel(new Rect(950f, 210f, 850f, 650f));
            GUI.Label(new Rect(1000f, 250f, 700f, 46f), "도심 구역", headingStyle);
            GUI.Label(new Rect(1000f, 330f, 700f, 180f),
                "남은 밤 동안 지상을 탐색하고\n개방 가능한 벙커를 찾아 돌아온다.\n\n사망하면 가지고 간 장비를 잃는다.", labelStyle);

            if (MenuButton(new Rect(120f, 935f, 260f, 62f), "뒤로"))
                OpenMapSelection();
            if (MenuButton(new Rect(1430f, 920f, 370f, 78f), "지상으로 이동"))
                BeginRaid();
        }

        private void DrawShelter()
        {
            DrawTopBar("은신처", "시설을 선택해 필요한 물품을 준비한다");
            DrawPanel(new Rect(170f, 240f, 1580f, 570f));
            GUI.Label(new Rect(230f, 290f, 620f, 46f), "시설", headingStyle);

            DrawFacility(new Rect(230f, 390f, 420f, 240f), "의료 장비 제작 시설");
            DrawFacility(new Rect(750f, 390f, 420f, 240f), "무기 제작 시설");
            DrawFacility(new Rect(1270f, 390f, 420f, 240f), "아이템 조합 시설");
            GUI.Label(new Rect(230f, 700f, 1200f, 40f), "현재 프로토타입에서는 시설 선택 화면까지만 연결되어 있다.", smallStyle);

            if (MenuButton(new Rect(120f, 935f, 260f, 62f), "메인으로"))
                OpenTitle();
        }

        private void DrawCharacterStorage()
        {
            DrawTopBar("캐릭터", "장착 장비와 창고");
            DrawPanel(new Rect(100f, 190f, 620f, 720f));
            DrawPanel(new Rect(770f, 190f, 1050f, 720f));
            GUI.Label(new Rect(150f, 230f, 500f, 46f), "장착 장비", headingStyle);
            GUI.Label(new Rect(820f, 230f, 700f, 46f), "창고", headingStyle);

            DrawEquipmentSlot(new Rect(150f, 315f, 520f, 68f), "총기", "Prototype Pistol");
            DrawEquipmentSlot(new Rect(150f, 400f, 250f, 110f), "근접무기", "Knife");
            DrawEquipmentSlot(new Rect(420f, 400f, 250f, 110f), "방어구", "Basic");
            DrawEquipmentSlot(new Rect(150f, 530f, 250f, 155f), "가방", "Small");
            DrawEquipmentSlot(new Rect(420f, 530f, 250f, 155f), "안전 포켓", "2 Slots");
            DrawEquipmentSlot(new Rect(150f, 705f, 250f, 110f), "퀵슬롯", "Bandage");

            const int columns = 8;
            const int rows = 5;
            const float size = 86f;
            const float gap = 14f;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Rect slot = new Rect(820f + column * (size + gap), 315f + row * (size + gap), size, size);
                    GUI.Box(slot, string.Empty, slotStyle);
                }
            }

            GUI.Label(new Rect(820f, 835f, 860f, 38f), "아이템 데이터 연결 전 임시 창고", smallStyle);
            if (MenuButton(new Rect(120f, 950f, 260f, 62f), "메인으로"))
                OpenTitle();
            if (MenuButton(new Rect(1490f, 950f, 330f, 62f), "PLAY"))
                OpenMapSelection();
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

        private void DrawMapLines()
        {
            DrawRect(new Rect(612f, 430f, 535f, 3f), new Color(0.32f, 0.37f, 0.36f, 0.75f));
            DrawRect(new Rect(1120f, 392f, 3f, 230f), new Color(0.32f, 0.37f, 0.36f, 0.75f));
            DrawRect(new Rect(825f, 660f, 480f, 3f), new Color(0.32f, 0.37f, 0.36f, 0.75f));
            DrawRect(new Rect(770f, 430f, 3f, 325f), new Color(0.32f, 0.37f, 0.36f, 0.75f));
        }

        private void DrawLoadoutRow(float x, float y, string slotName, string itemName)
        {
            GUI.Box(new Rect(x, y, 650f, 64f), string.Empty, slotStyle);
            GUI.Label(new Rect(x + 20f, y + 10f, 180f, 44f), slotName, smallStyle);
            GUI.Label(new Rect(x + 210f, y + 10f, 410f, 44f), itemName, labelStyle);
        }

        private void DrawEquipmentSlot(Rect rect, string slotName, string itemName)
        {
            GUI.Box(rect, string.Empty, slotStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 28f, 30f), slotName, smallStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 39f, rect.width - 28f, rect.height - 44f), itemName, labelStyle);
        }

        private void DrawFacility(Rect rect, string facilityName)
        {
            GUI.Box(rect, string.Empty, slotStyle);
            GUI.Label(new Rect(rect.x + 25f, rect.y + 25f, rect.width - 50f, rect.height - 50f), facilityName, headingStyle);
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
