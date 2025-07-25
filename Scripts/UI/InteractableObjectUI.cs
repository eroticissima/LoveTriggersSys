using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using LTSystem.Objects;
//using DG.Tweening; // Optional: for smooth animations

namespace LTSystem.Objects
{
    /// <summary>
    /// UI panel that appears when players approach interactable objects.
    /// Shows context-specific love triggers like "Sit" for chairs or "Cuddle" for beds.
    /// </summary>
    public class InteractableObjectUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelContainer;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject buttonPrefab;

        [Header("Layout")]
        [SerializeField] private float buttonSpacing = 10f;
        [SerializeField] private float buttonWidth = 200f;
        [SerializeField] private float buttonHeight = 50f;
        [SerializeField] private bool autoArrangeButtons = true;
       // [SerializeField] private float autoArrangeButtons = true;
        [SerializeField] private LayoutStyle layoutStyle = LayoutStyle.Vertical;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool animateButtons = true;
        [SerializeField] private float buttonAnimationDelay = 0.05f;

        [Header("World Space Settings")]
        [SerializeField] private bool worldSpaceUI = true;
        [SerializeField] private float uiScale = 0.01f;
        [SerializeField] private bool billboardMode = true;
        //[SerializeField] private float distanceScaling = false;
        [SerializeField] private bool distanceScaling = false;
        [SerializeField] private Vector2 minMaxScale = new Vector2(0.5f, 1.5f);

        [Header("Visual Style")]
        [SerializeField] public Color normalButtonColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] public Color hoverButtonColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color pressedButtonColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color disabledButtonColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private TMP_FontAsset buttonFont;

        [Header("Icons")]
        [SerializeField] private bool showIcons = true;
        [SerializeField] private float iconSize = 32f;
        [SerializeField] private IconPosition iconPosition = IconPosition.Left;

        // Runtime references
        private InteractableObject interactableObject;
        private PlayerController playerController;
        private Camera playerCamera;
        private List<LoveTriggerButton> createdButtons = new List<LoveTriggerButton>();
        private bool isVisible = false;
        private Coroutine currentAnimation;

        public enum LayoutStyle
        {
            Vertical,
            Horizontal,
            Grid,
            Radial
        }

        public enum IconPosition
        {
            Left,
            Right,
            Top,
            Bottom
        }

        #region Initialization

        void Awake()
        {
            SetupCanvas();
            Hide(true); // Start hidden
        }

        void SetupCanvas()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            // Configure for world space
            if (worldSpaceUI)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.localScale = Vector3.one * uiScale;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            // Setup canvas group for fading
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Setup raycaster
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        public void Setup(InteractableObject interactable, PlayerController player, LoveTriggerSO[] triggers)
        {
            interactableObject = interactable;
            playerController = player;
            playerCamera = Camera.main;

            // Set canvas camera
            if (!worldSpaceUI && playerCamera != null)
            {
                canvas.worldCamera = playerCamera;
            }

            // Create buttons for each trigger
            CreateButtons(triggers);

            // Show with animation
            Show();
        }

        #endregion

        #region Button Creation

        void CreateButtons(LoveTriggerSO[] triggers)
        {
            // Clear existing buttons
            ClearButtons();

            if (triggers == null || triggers.Length == 0) return;

            // Create button for each trigger
            for (int i = 0; i < triggers.Length; i++)
            {
                if (triggers[i] != null)
                {
                    CreateButton(triggers[i], i);
                }
            }

            // Arrange buttons
            if (autoArrangeButtons)
            {
                ArrangeButtons();
            }
        }

        void CreateButton(LoveTriggerSO trigger, int index)
        {
            GameObject buttonObj = buttonPrefab != null ?
                Instantiate(buttonPrefab, buttonContainer) :
                CreateDefaultButton();

            // Setup button component
            var loveTriggerButton = buttonObj.GetComponent<LoveTriggerButton>();
            if (loveTriggerButton == null)
            {
                loveTriggerButton = buttonObj.AddComponent<LoveTriggerButton>();
            }

            loveTriggerButton.Setup(trigger, this, index);
            createdButtons.Add(loveTriggerButton);

            // Initial state
            if (animateButtons)
            {
                buttonObj.transform.localScale = Vector3.zero;
            }
        }

        GameObject CreateDefaultButton()
        {
            // Create a default button if no prefab is assigned
            GameObject buttonObj = new GameObject("LoveTriggerButton");
            buttonObj.transform.SetParent(buttonContainer);

            // Add RectTransform
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            // Add Image component
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = normalButtonColor;

            // Add Button component
            Button button = buttonObj.AddComponent<Button>();

            // Create text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Trigger";
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 18;

            if (buttonFont != null)
                text.font = buttonFont;

            return buttonObj;
        }

        #endregion

        #region Layout Management

        void ArrangeButtons()
        {
            switch (layoutStyle)
            {
                case LayoutStyle.Vertical:
                    ArrangeVertical();
                    break;
                case LayoutStyle.Horizontal:
                    ArrangeHorizontal();
                    break;
                case LayoutStyle.Grid:
                    ArrangeGrid();
                    break;
                case LayoutStyle.Radial:
                    ArrangeRadial();
                    break;
            }
        }

        void ArrangeVertical()
        {
            float totalHeight = createdButtons.Count * buttonHeight + (createdButtons.Count - 1) * buttonSpacing;
            float startY = totalHeight / 2f;

            for (int i = 0; i < createdButtons.Count; i++)
            {
                RectTransform rect = createdButtons[i].GetComponent<RectTransform>();
                float yPos = startY - (i * (buttonHeight + buttonSpacing));
                rect.anchoredPosition = new Vector2(0, yPos);
            }
        }

        void ArrangeHorizontal()
        {
            float totalWidth = createdButtons.Count * buttonWidth + (createdButtons.Count - 1) * buttonSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < createdButtons.Count; i++)
            {
                RectTransform rect = createdButtons[i].GetComponent<RectTransform>();
                float xPos = startX + (i * (buttonWidth + buttonSpacing)) + buttonWidth / 2f;
                rect.anchoredPosition = new Vector2(xPos, 0);
            }
        }

        void ArrangeGrid()
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(createdButtons.Count));
            int rows = Mathf.CeilToInt((float)createdButtons.Count / columns);

            float gridWidth = columns * buttonWidth + (columns - 1) * buttonSpacing;
            float gridHeight = rows * buttonHeight + (rows - 1) * buttonSpacing;

            float startX = -gridWidth / 2f + buttonWidth / 2f;
            float startY = gridHeight / 2f - buttonHeight / 2f;

            for (int i = 0; i < createdButtons.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;

                RectTransform rect = createdButtons[i].GetComponent<RectTransform>();
                float xPos = startX + col * (buttonWidth + buttonSpacing);
                float yPos = startY - row * (buttonHeight + buttonSpacing);
                rect.anchoredPosition = new Vector2(xPos, yPos);
            }
        }

        void ArrangeRadial()
        {
            float radius = 150f;
            float angleStep = 360f / createdButtons.Count;
            float startAngle = 90f; // Start from top

            for (int i = 0; i < createdButtons.Count; i++)
            {
                float angle = (startAngle - i * angleStep) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;

                RectTransform rect = createdButtons[i].GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(x, y);

                // Rotate button to face outward
                rect.rotation = Quaternion.Euler(0, 0, startAngle - i * angleStep - 90f);
            }
        }

        #endregion

        #region Visibility

        public void Show()
        {
            if (isVisible) return;

            gameObject.SetActive(true);
            isVisible = true;

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            currentAnimation = StartCoroutine(ShowAnimation());
        }

        public void Hide(bool immediate = false)
        {
            if (!isVisible && !immediate) return;

            isVisible = false;

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            if (immediate)
            {
                canvasGroup.alpha = 0;
                gameObject.SetActive(false);
            }
            else
            {
                currentAnimation = StartCoroutine(HideAnimation());
            }
        }

        IEnumerator ShowAnimation()
        {
            // Fade in
            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                canvasGroup.alpha = fadeInCurve.Evaluate(t);
                yield return null;
            }

            canvasGroup.alpha = 1;

            // Animate buttons
            if (animateButtons)
            {
                for (int i = 0; i < createdButtons.Count; i++)
                {
                    StartCoroutine(AnimateButtonIn(createdButtons[i], i * buttonAnimationDelay));
                }
            }
        }

        IEnumerator HideAnimation()
        {
            // Fade out
            float elapsed = 0;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1 - (elapsed / fadeOutDuration);
                canvasGroup.alpha = t;
                yield return null;
            }

            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
        }

        IEnumerator AnimateButtonIn(LoveTriggerButton button, float delay)
        {
            yield return new WaitForSeconds(delay);

            Transform buttonTransform = button.transform;

#if DOTWEEN_ENABLED
            buttonTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
#else
            // Manual animation
            float elapsed = 0;
            float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                buttonTransform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
                yield return null;
            }

            buttonTransform.localScale = Vector3.one;
#endif
        }

        #endregion

        #region Updates

        void Update()
        {
            if (!isVisible) return;

            // Billboard mode
            if (billboardMode && playerCamera != null)
            {
                transform.LookAt(transform.position + playerCamera.transform.rotation * Vector3.forward,
                    playerCamera.transform.rotation * Vector3.up);
            }

            // Distance scaling
            if (distanceScaling && playerCamera != null)
            {
                float distance = Vector3.Distance(transform.position, playerCamera.transform.position);
                float scaleFactor = Mathf.Clamp(10f / distance, minMaxScale.x, minMaxScale.y);
                transform.localScale = Vector3.one * uiScale * scaleFactor;
            }
        }

        public void StartFacingCamera(Camera camera)
        {
            playerCamera = camera;
            billboardMode = true;
        }

        #endregion

        #region Button Callbacks

        public void OnButtonClicked(LoveTriggerSO trigger)
        {
            if (interactableObject != null && playerController != null)
            {
                interactableObject.ExecuteTrigger(trigger.triggerID, playerController);
                Hide();
            }
        }

        public void OnButtonHovered(LoveTriggerButton button, bool isHovered)
        {
            // Could show tooltip or additional info
        }

        #endregion

        #region Cleanup

        void ClearButtons()
        {
            foreach (var button in createdButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }

            createdButtons.Clear();
        }

        void OnDestroy()
        {
            ClearButtons();

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);
        }

        #endregion
    }

    /// <summary>
    /// Individual button component for love triggers
    /// </summary>
    public class LoveTriggerButton : MonoBehaviour
    {
        [Header("Components")]
        private Button button;
        private Image backgroundImage;
        private TMP_Text label;
        private Image iconImage;

        // References
        private LoveTriggerSO trigger;
        private InteractableObjectUI parentUI;
        private int index;

        // State
        private bool isHovered = false;

        public void Setup(LoveTriggerSO triggerData, InteractableObjectUI ui, int buttonIndex)
        {
            trigger = triggerData;
            parentUI = ui;
            index = buttonIndex;

            // Get components
            button = GetComponent<Button>();
            backgroundImage = GetComponent<Image>();
            label = GetComponentInChildren<TMP_Text>();

            // Find icon if exists
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();

            // Configure button
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }

            // Set text
            if (label != null && trigger != null)
            {
                label.text = trigger.triggerName;
            }

            // Set icon
            if (iconImage != null && trigger != null && trigger.icon != null)
            {
                iconImage.sprite = trigger.icon;
                iconImage.gameObject.SetActive(true);
            }

            // Setup hover detection
            SetupHoverDetection();
        }

        void SetupHoverDetection()
        {
            var eventTrigger = gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
                eventTrigger = gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            // Pointer Enter
            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((data) => OnPointerEnter());
            eventTrigger.triggers.Add(pointerEnter);

            // Pointer Exit
            var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) => OnPointerExit());
            eventTrigger.triggers.Add(pointerExit);
        }

        void OnClick()
        {
            if (parentUI != null && trigger != null)
            {
                parentUI.OnButtonClicked(trigger);
            }
        }

        void OnPointerEnter()
        {
            isHovered = true;
            if (parentUI != null)
            {
                parentUI.OnButtonHovered(this, true);
            }

            // Visual feedback
            if (backgroundImage != null)
            {
                backgroundImage.color = parentUI.hoverButtonColor;
            }
        }

        void OnPointerExit()
        {
            isHovered = false;
            if (parentUI != null)
            {
                parentUI.OnButtonHovered(this, false);
            }

            // Visual feedback
            if (backgroundImage != null)
            {
                backgroundImage.color = parentUI.normalButtonColor;
            }
        }

        public LoveTriggerSO GetTrigger() => trigger;
        public int GetIndex() => index;
        public bool IsHovered() => isHovered;
    }
}