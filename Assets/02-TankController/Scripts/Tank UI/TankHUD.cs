using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]

public class TankHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TankUIEvents m_UIEvents;
    [SerializeField] private TankWeapon m_Weapon;

    [Header("Placement")]
    [SerializeField] private int m_Margin = 14;

    // --------- SCALE CONTROLS (change these) ---------
    [Header("Scale (adjust these)")]
    [SerializeField] private float m_UIScale = 1.25f;          // <— MAIN knob (try 1.2–1.6)
    [SerializeField] private float m_CardMinWidth = 240f;      // <— makes cards wider [web:428]
    [SerializeField] private float m_CardPaddingX = 18f;       // <— inner width padding
    [SerializeField] private float m_CardPaddingY = 14f;       // <— inner height padding
    [SerializeField] private float m_LineSpacing = 7f;         // <— vertical spacing between small lines
    // -------------------------------------------------

    [Header("Theme")]
    [SerializeField] private Color m_PanelBg = new(0.05f, 0.07f, 0.10f, 0.72f);
    [SerializeField] private Color m_Accent = new(0.36f, 0.74f, 1.00f, 1.00f);
    [SerializeField] private Color m_Text = Color.white;

    [Header("Weapon Colours")]
    [SerializeField] private Color m_WeaponReady = new(0.20f, 0.90f, 0.25f, 1f);
    [SerializeField] private Color m_WeaponReload = new(0.95f, 0.55f, 0.10f, 1f);

    [Header("Weapon Segments")]
    [SerializeField] private int m_SegmentCount = 10;
    [SerializeField] private float m_SegmentWidth = 18f;
    [SerializeField] private float m_SegmentHeight = 10f;
    [SerializeField] private float m_SegmentGap = 4f;

    [SerializeField] private float m_SlipGap = 10f; // space between slip box and weapon box

    private VisualElement m_Root;

    private VisualElement m_LeftCard;
    private Label m_SpeedBig;
    private Label m_SpeedUnits;
    private Label m_DirectionLine;
    private Label m_HeadingLine;
    private Label m_ReverseBadge;

    private VisualElement m_SlipCard;
    private Label m_SlipLabel;

    private VisualElement m_RightCard;
    private Label m_WeaponTitle;
    private Label m_WeaponStatus;
    private Label m_WeaponCountdown;
    private Label m_ElevationLine;

    private VisualElement m_SegmentsRow;
    private VisualElement[] m_Segments;

    private IVisualElementScheduledItem m_ReloadScheduler;

    private void OnEnable()
    {
        m_Root = GetComponent<UIDocument>().rootVisualElement;
        m_Root.Clear();

        BuildLayout();
        HookEvents();

        SetSpeed(0f);
        SetDirection(TankUIEvents.MoveDir.Idle);
        SetHeading(0f);
        SetElevation(0f);
        RefreshWeaponUI(force: true);
    }

    private void OnDisable()
    {
        UnhookEvents();
        m_ReloadScheduler?.Pause();
        m_ReloadScheduler = null;
    }

    private void BuildLayout()
    {
        // LEFT
        m_LeftCard = MakeCard();
        m_LeftCard.style.left = m_Margin;
        m_LeftCard.style.bottom = m_Margin;
        ApplyCardSizing(m_LeftCard);

        var speedRow = new VisualElement();
        speedRow.style.flexDirection = FlexDirection.Row;
        speedRow.style.alignItems = Align.FlexEnd;

        m_SpeedBig = new Label("0.0");
        m_SpeedBig.style.fontSize = Mathf.RoundToInt(36 * m_UIScale);
        m_SpeedBig.style.color = m_Text;
        m_SpeedBig.style.unityFontStyleAndWeight = FontStyle.Bold;

        m_SpeedUnits = new Label("m/s");
        m_SpeedUnits.style.fontSize = Mathf.RoundToInt(14 * m_UIScale);
        m_SpeedUnits.style.color = new Color(m_Text.r, m_Text.g, m_Text.b, 0.85f);
        m_SpeedUnits.style.marginLeft = 8 * m_UIScale;
        m_SpeedUnits.style.marginBottom = 6 * m_UIScale;

        speedRow.Add(m_SpeedBig);
        speedRow.Add(m_SpeedUnits);

        m_DirectionLine = MakeLine("Motion: Idle");
        m_HeadingLine = MakeLine("HDG: 0°");

        m_ReverseBadge = new Label("REV");
        m_ReverseBadge.style.display = DisplayStyle.None;
        m_ReverseBadge.style.marginTop = 8 * m_UIScale;
        m_ReverseBadge.style.paddingLeft = 10 * m_UIScale;
        m_ReverseBadge.style.paddingRight = 10 * m_UIScale;
        m_ReverseBadge.style.paddingTop = 4 * m_UIScale;
        m_ReverseBadge.style.paddingBottom = 4 * m_UIScale;
        m_ReverseBadge.style.backgroundColor = new Color(0.85f, 0.15f, 0.15f, 0.95f);
        m_ReverseBadge.style.color = Color.white;
        m_ReverseBadge.style.borderTopLeftRadius = 6;
        m_ReverseBadge.style.borderTopRightRadius = 6;
        m_ReverseBadge.style.borderBottomLeftRadius = 6;
        m_ReverseBadge.style.borderBottomRightRadius = 6;

        m_LeftCard.Add(speedRow);
        m_LeftCard.Add(m_DirectionLine);
        m_LeftCard.Add(m_HeadingLine);
        m_LeftCard.Add(m_ReverseBadge);

        // RIGHT
        m_RightCard = MakeCard();
        m_RightCard.style.right = m_Margin;
        m_RightCard.style.bottom = m_Margin;
        ApplyCardSizing(m_RightCard);

        m_WeaponTitle = new Label("WEAPON");
        m_WeaponTitle.style.fontSize = Mathf.RoundToInt(14 * m_UIScale);
        m_WeaponTitle.style.color = new Color(m_Text.r, m_Text.g, m_Text.b, 0.8f);
        m_WeaponTitle.style.unityTextAlign = TextAnchor.MiddleRight;

        m_WeaponStatus = new Label("READY");
        m_WeaponStatus.style.fontSize = Mathf.RoundToInt(20 * m_UIScale);
        m_WeaponStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
        m_WeaponStatus.style.unityTextAlign = TextAnchor.MiddleRight;

        m_WeaponCountdown = new Label("");
        m_WeaponCountdown.style.fontSize = Mathf.RoundToInt(14 * m_UIScale);
        m_WeaponCountdown.style.unityTextAlign = TextAnchor.MiddleRight;
        m_WeaponCountdown.style.color = new Color(m_Text.r, m_Text.g, m_Text.b, 0.85f);
        m_WeaponCountdown.style.marginTop = m_LineSpacing;

        m_ElevationLine = new Label("ELV: 0°");
        m_ElevationLine.style.marginTop = m_LineSpacing;
        m_ElevationLine.style.fontSize = Mathf.RoundToInt(14 * m_UIScale);
        m_ElevationLine.style.unityTextAlign = TextAnchor.MiddleRight;
        m_ElevationLine.style.color = new Color(m_Text.r, m_Text.g, m_Text.b, 0.9f);

        m_SegmentsRow = new VisualElement();
        m_SegmentsRow.style.flexDirection = FlexDirection.Row;
        m_SegmentsRow.style.justifyContent = Justify.FlexEnd;
        m_SegmentsRow.style.marginTop = 8 * m_UIScale;

        int count = Mathf.Max(3, m_SegmentCount);
        m_Segments = new VisualElement[count];

        for (int i = 0; i < count; i++)
        {
            var seg = new VisualElement();
            seg.style.width = m_SegmentWidth * m_UIScale;
            seg.style.height = m_SegmentHeight * m_UIScale;

            if (i != count - 1)
                seg.style.marginRight = m_SegmentGap * m_UIScale;

            seg.style.borderTopLeftRadius = 3;
            seg.style.borderTopRightRadius = 3;
            seg.style.borderBottomLeftRadius = 3;
            seg.style.borderBottomRightRadius = 3;

            seg.style.backgroundColor = new Color(0.20f, 0.26f, 0.32f, 1f);

            m_SegmentsRow.Add(seg);
            m_Segments[i] = seg;
        }

        m_RightCard.Add(m_WeaponTitle);
        m_RightCard.Add(m_WeaponStatus);
        m_RightCard.Add(m_WeaponCountdown);
        m_RightCard.Add(m_ElevationLine);
        m_RightCard.Add(m_SegmentsRow);

        m_Root.Add(m_LeftCard);
        m_Root.Add(m_RightCard);
    }

    private void ApplyCardSizing(VisualElement card)
    {
        // Make cards wider and give them more internal padding.
        card.style.minWidth = m_CardMinWidth * m_UIScale; // supported via IStyle.minWidth [web:428]

        card.style.paddingLeft = m_CardPaddingX * m_UIScale;
        card.style.paddingRight = m_CardPaddingX * m_UIScale;
        card.style.paddingTop = m_CardPaddingY * m_UIScale;
        card.style.paddingBottom = m_CardPaddingY * m_UIScale;
    }

    private VisualElement MakeCard()
    {
        var card = new VisualElement();
        card.style.position = Position.Absolute;

        // default padding gets overridden by ApplyCardSizing()
        card.style.paddingLeft = 14;
        card.style.paddingRight = 14;
        card.style.paddingTop = 12;
        card.style.paddingBottom = 12;

        card.style.backgroundColor = m_PanelBg;

        card.style.borderTopLeftRadius = 10;
        card.style.borderTopRightRadius = 10;
        card.style.borderBottomLeftRadius = 10;
        card.style.borderBottomRightRadius = 10;

        card.style.borderLeftWidth = 3;
        card.style.borderLeftColor = m_Accent;

        return card;
    }

    private Label MakeLine(string text)
    {
        var l = new Label(text);
        l.style.marginTop = m_LineSpacing;
        l.style.fontSize = Mathf.RoundToInt(14 * m_UIScale);
        l.style.color = new Color(m_Text.r, m_Text.g, m_Text.b, 0.9f);
        return l;
    }

    private void HookEvents()
    {
        if (m_UIEvents == null) return;

        m_UIEvents.SpeedChanged += OnSpeedChanged;
        m_UIEvents.DirectionChanged += OnDirectionChanged;
        m_UIEvents.HeadingChanged += OnHeadingChanged;
        m_UIEvents.GunElevationChanged += OnGunElevationChanged;

        m_UIEvents.WeaponReadyChanged += OnWeaponReadyChanged;
        m_UIEvents.WeaponFired += OnWeaponFired;
        m_UIEvents.SlippingChanged += OnSlippingChanged;

    }

    private void UnhookEvents()
    {
        if (m_UIEvents == null) return;

        m_UIEvents.SpeedChanged -= OnSpeedChanged;
        m_UIEvents.DirectionChanged -= OnDirectionChanged;
        m_UIEvents.HeadingChanged -= OnHeadingChanged;
        m_UIEvents.GunElevationChanged -= OnGunElevationChanged;

        m_UIEvents.WeaponReadyChanged -= OnWeaponReadyChanged;
        m_UIEvents.WeaponFired -= OnWeaponFired;
        m_UIEvents.SlippingChanged -= OnSlippingChanged;

    }

    private void OnSlippingChanged(bool slipping)
    {
        if (slipping)
        {
            if (m_SlipCard != null) return;

            m_SlipCard = MakeCard();
            ApplyCardSizing(m_SlipCard);

            m_SlipCard.style.right = m_Margin;
            m_SlipCard.style.bottom = m_Margin + (110 * m_UIScale); // push it above weapon card (tweak)
            m_SlipCard.style.borderLeftColor = new Color(1.0f, 0.55f, 0.10f, 1f);

            m_SlipLabel = new Label("SLIPPING");
            m_SlipLabel.style.fontSize = Mathf.RoundToInt(18 * m_UIScale);
            m_SlipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_SlipLabel.style.color = Color.white;

            m_SlipCard.Add(m_SlipLabel);
            m_Root.Add(m_SlipCard);

            PositionSlipCardAboveWeapon();

            // then position again next frame after layout resolves. [web:390][web:427]
            m_Root.schedule.Execute(PositionSlipCardAboveWeapon);
        }
        else
        {
            if (m_SlipCard == null) return;
            m_SlipCard.RemoveFromHierarchy();
            m_SlipCard = null;
            m_SlipLabel = null;
        }
    }

    private void PositionSlipCardAboveWeapon()
    {
        if (m_SlipCard == null || m_RightCard == null) return;

        float weaponH = m_RightCard.resolvedStyle.height; // final layout height [web:427]
        if (weaponH <= 0.1f || float.IsNaN(weaponH)) return;

        m_SlipCard.style.right = m_Margin;
        m_SlipCard.style.bottom = m_Margin + weaponH + (m_SlipGap * m_UIScale);
    }
    private void OnSpeedChanged(float speed) => SetSpeed(speed);
    private void OnDirectionChanged(TankUIEvents.MoveDir dir) => SetDirection(dir);
    private void OnHeadingChanged(float headingDeg) => SetHeading(headingDeg);
    private void OnGunElevationChanged(float elevationDeg) => SetElevation(elevationDeg);

    private void OnWeaponReadyChanged(bool ready)
    {
        RefreshWeaponUI(force: true);

        if (ready) m_ReloadScheduler?.Pause();
        else
        {
            EnsureReloadScheduler();
            m_ReloadScheduler.Resume();
        }
    }

    private void OnWeaponFired()
    {
        RefreshWeaponUI(force: true);
        EnsureReloadScheduler();
        m_ReloadScheduler.Resume();
    }

    private void SetSpeed(float speed)
    {
        if (m_SpeedBig != null)
            m_SpeedBig.text = speed.ToString("0.0");
    }

    private void SetDirection(TankUIEvents.MoveDir dir)
    {
        if (m_DirectionLine != null)
            m_DirectionLine.text = $"Motion: {dir}";

        if (m_ReverseBadge != null)
            m_ReverseBadge.style.display = (dir == TankUIEvents.MoveDir.Reverse) ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetHeading(float headingDeg)
    {
        if (m_HeadingLine != null)
            m_HeadingLine.text = $"HDG: {headingDeg:0}°";
    }

    private void SetElevation(float elevationDeg)
    {
        if (m_ElevationLine == null) return;

        m_ElevationLine.text = $"ELV: {elevationDeg:+0;-0;0}°";
        m_ElevationLine.style.display = Mathf.Abs(elevationDeg) < 0.5f ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void EnsureReloadScheduler()
    {
        if (m_ReloadScheduler != null) return;

        m_ReloadScheduler = m_Root.schedule.Execute(() => RefreshWeaponUI(force: false)).Every(33);
    }

    private void RefreshWeaponUI(bool force)
    {
        if (m_Weapon == null || m_WeaponStatus == null || m_Segments == null)
        {
            if (m_WeaponStatus != null) m_WeaponStatus.text = "NO WEAPON";
            return;
        }

        bool ready = m_Weapon.IsReady;
        float t = m_Weapon.Cooldown01;

        m_WeaponStatus.text = ready ? "ARMED" : "COOLING";
        m_WeaponStatus.style.color = ready ? m_WeaponReady : m_WeaponReload;

        if (ready)
        {
            m_WeaponCountdown.text = "";
        }
        else
        {
            float remaining = Mathf.Max(0f, m_Weapon.m_FireCooldown * (1f - t)); // <-- FIXED
            m_WeaponCountdown.text = $"{remaining:0.0}s";
        }

        int filled = Mathf.Clamp(Mathf.RoundToInt(t * m_Segments.Length), 0, m_Segments.Length);
        Color filledCol = ready ? m_WeaponReady : m_WeaponReload;

        for (int i = 0; i < m_Segments.Length; i++)
        {
            bool on = i < filled;
            m_Segments[i].style.backgroundColor = on ? filledCol : new Color(0.20f, 0.26f, 0.32f, 1f);
            m_Segments[i].style.opacity = on ? 1f : 0.55f;
        }

        if (ready)
            m_ReloadScheduler?.Pause();
    }
}
