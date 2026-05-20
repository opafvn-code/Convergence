using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FadePlanet
{
    internal class UI
    {
        // =========================
        //        VARIABLES
        // =========================

        public float MaxHealth { get; private set; } = 100f;
        public float CurrentHealth { get; private set; } = 100f;

        public float MaxStamina { get; private set; } = 100f;
        public float CurrentStamina { get; private set; } = 100f;

        public int PotionCount { get; private set; } = 0;
        public int CurrencyCount { get; private set; } = 0;

        // =========================
        //    INVENTORY SETTINGS
        // =========================
        private const int InventoryScale = 2;
        private const int InventoryOriginalW = 208;
        private const int InventoryOriginalH = 44;
        private const int InventoryDrawW = InventoryOriginalW * InventoryScale;
        private const int InventoryDrawH = InventoryOriginalH * InventoryScale;
        private const int InventoryMargin = 16;

        // =========================
        //    ITEM ICON SETTINGS
        // =========================
        private const float ItemIconScale = 1.8f;
        private const int ItemIconSize = (int)(32 * ItemIconScale);

        // -----------------------------------------------------------------------
        // INDIVIDUAL ICON POSITIONS
        // Each icon has its own X and Y offset from the inventory bar's top-left
        // Positive X = further right, Positive Y = further down
        // Adjust these to place each icon exactly where you want it
        // -----------------------------------------------------------------------
        private const float Slot1X = 10f; private const float Slot1Y = 16f;  // Water Scroll
        private const float Slot2X = 67f; private const float Slot2Y = 16f;  // Fire Scroll
        private const float Slot3X = 122f; private const float Slot3Y = 16f;  // Earth Scroll
        private const float Slot4X = 179f; private const float Slot4Y = 16f;  // Air Scroll
        private const float Slot5X = 238f; private const float Slot5Y = 16f;  // Sword
        private const float Slot6X = 290f; private const float Slot6Y = 16f;  // Potion
        private const float Slot7X = 347f; private const float Slot7Y = 16f;  // Currency 
        // -----------------------------------------------------------------------

        private const int SlotCount = 6; // 7th is currency, doesnt need to be selected

        // =========================
        //    HIGHLIGHT BOX SETTINGS
        // =========================
        private const float HighlightBoxWidth = 50f;
        private const float HighlightBoxHeight = 55f;
        private const float HighlightSlideSpeed = 10f;
        private const float HighlightThickness = 5f;

        private float highlightCurrentX = 0f;
        private float highlightCurrentY = 0f;
        private float highlightTargetX = 0f;
        private float highlightTargetY = 0f;
        private bool highlightInitialized = false;

        // Which slot is currently selected (0-indexed)
        private int selectedSlot = 4; // Default = slot 5 (sword)

        // 0=Water, 1=Fire, 2=Earth, 3=Air, 4=Sword, 5=Potion
        public int SelectedSlot => selectedSlot;

        // Stores a scroll slot press that came in while locked
        // -1 means no pending slot
        private int pendingSlot = -1;

        // =========================
        //    SCROLL DISPLAY SETTINGS
        // =========================
        private const int ScrollFrameSize = 32;
        private const int ScrollTotalFrames = 7;
        private const int ScrollScale = 4;
        private const int ScrollDrawSize = ScrollFrameSize * ScrollScale;
        private const int ScrollMargin = 16;
        private const int ScrollFrameDuration = 2;

        // Scroll spritesheets
        private Bitmap airScrollSheet;
        private Bitmap earthScrollSheet;
        private Bitmap fireScrollSheet;
        private Bitmap waterScrollSheet;

        // Inventory icons
        private Bitmap waterScrollIcon;
        private Bitmap fireScrollIcon;
        private Bitmap earthScrollIcon;
        private Bitmap airScrollIcon;
        private Image swordIcon;
        private Image potionIcon;
        private Image currencyIcon;

        // Scroll animation state
        private enum ScrollAnimState { Idle, Closing, Opening }
        private ScrollAnimState scrollState = ScrollAnimState.Idle;
        private int scrollFrameIndex = 0;
        private int scrollFrameTimer = 0;

        private Bitmap currentScrollSheet;
        private Bitmap pendingScrollSheet;

        public UI (string basePath)
        {
            LoadScrollSheets(basePath);
            LoadInventoryIcons(basePath);
        }

        // =========================
        //      LOAD METHODS
        // =========================

        public void LoadScrollSheets(string projectRoot)
        {
            try
            {
                airScrollSheet = new Bitmap(Path.Combine(projectRoot, @"Graphics\Items\Scrolls\AirScroll.png"));
                earthScrollSheet = new Bitmap(Path.Combine(projectRoot, @"Graphics\Items\Scrolls\EarthScroll.png"));
                fireScrollSheet = new Bitmap(Path.Combine(projectRoot, @"Graphics\Items\Scrolls\FireScroll.png"));
                waterScrollSheet = new Bitmap(Path.Combine(projectRoot, @"Graphics\Items\Scrolls\WaterScroll.png"));

                currentScrollSheet = airScrollSheet;

                waterScrollIcon = CropFrame(waterScrollSheet);
                fireScrollIcon = CropFrame(fireScrollSheet);
                earthScrollIcon = CropFrame(earthScrollSheet);
                airScrollIcon = CropFrame(airScrollSheet);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load scroll sheets: " + ex.Message);
            }
        }

        public void LoadInventoryIcons(string projectRoot)
        {
            try
            {
                swordIcon = Image.FromFile(Path.Combine(projectRoot, @"Graphics\Items\ElementSword.png"));
                potionIcon = Image.FromFile(Path.Combine(projectRoot, @"Graphics\Items\HealthPotion.png"));
                currencyIcon = Image.FromFile(Path.Combine(projectRoot, @"Graphics\UI\Currency.png"));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load inventory icons: " + ex.Message);
            }
        }

        private Bitmap CropFrame(Bitmap sheet)
        {
            Bitmap frame = new Bitmap(ScrollFrameSize, ScrollFrameSize);
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.DrawImage(sheet,
                    new Rectangle(0, 0, ScrollFrameSize, ScrollFrameSize),
                    new Rectangle(0, 0, ScrollFrameSize, ScrollFrameSize),
                    GraphicsUnit.Pixel);
            }
            return frame;
        }



        // =========================
        //    INVENTORY SLOT SELECT
        // =========================

        public void SetSelectedSlot(int slot, bool scrollLocked)
        {
            bool isScrollSlot = slot >= 1 && slot <= 4;

            if (isScrollSlot && scrollLocked)
            {
                // Store the slot press so we can apply it the moment the cooldown ends
                pendingSlot = slot - 1;
                return;
            }

            selectedSlot = slot - 1;
            pendingSlot = -1; // Clear any pending since we moved freely
        }

        // Called by Convergence when the scroll animation finishes
        // so the highlight catches up if a slot was pressed during the cooldown
        public void FlushPendingSlot()
        {
            if (pendingSlot != -1)
            {
                selectedSlot = pendingSlot;
                pendingSlot = -1;
            }
        }



        // =========================
        //    SCROLL SWITCH
        // =========================

        public void StartScrollSwitch(ElementType newElement)
        {
            switch (newElement)
            {
                case ElementType.Air: pendingScrollSheet = airScrollSheet; break;
                case ElementType.Earth: pendingScrollSheet = earthScrollSheet; break;
                case ElementType.Fire: pendingScrollSheet = fireScrollSheet; break;
                case ElementType.Water: pendingScrollSheet = waterScrollSheet; break;
            }

            scrollState = ScrollAnimState.Closing;
            scrollFrameIndex = 0;
            scrollFrameTimer = 0;
        }



        // =========================
        //      UPDATE METHODS
        // =========================

        public void UpdateHealth(float currentHealth, float maxHealth)
        {
            CurrentHealth = Math.Max(0, Math.Min(currentHealth, maxHealth));
            MaxHealth = maxHealth;
        }

        public void UpdateStamina(float currentStamina, float maxStamina)
        {
            CurrentStamina = Math.Max(0, Math.Min(currentStamina, maxStamina));
            MaxStamina = maxStamina;
        }

        public void UpdatePotionCount(int potionCount)
        {
            PotionCount = Math.Max(0, potionCount);
        }
        public void UpdateCurrency(int currencyCount)
        {
            CurrencyCount = Math.Max(0, currencyCount);
        }

        public (bool closingDone, bool openingDone) UpdateScrollAnimation()
        {
            bool closingDone = false;
            bool openingDone = false;

            if (scrollState == ScrollAnimState.Idle) return (false, false);

            scrollFrameTimer++;

            if (scrollFrameTimer >= ScrollFrameDuration)
            {
                scrollFrameTimer = 0;

                if (scrollState == ScrollAnimState.Closing)
                {
                    scrollFrameIndex++;
                    if (scrollFrameIndex >= ScrollTotalFrames)
                    {
                        currentScrollSheet = pendingScrollSheet;
                        scrollFrameIndex = ScrollTotalFrames - 1;
                        scrollState = ScrollAnimState.Opening;
                        closingDone = true;
                    }
                }
                else if (scrollState == ScrollAnimState.Opening)
                {
                    scrollFrameIndex--;
                    if (scrollFrameIndex < 0)
                    {
                        scrollFrameIndex = 0;
                        scrollState = ScrollAnimState.Idle;
                        openingDone = true;
                    }
                }
            }

            return (closingDone, openingDone);
        }



        // =========================
        //       DRAW METHOD
        // =========================

        public void DrawWinFormsUI(Graphics g, Image hGraphic, Image hBar, Image sGraphic, Image sBar, Image inventorySlots, int screenWidth, int screenHeight)
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            int scale = 2;

            float healthPercent = CurrentHealth / MaxHealth;
            float staminaPercent = CurrentStamina / MaxStamina;

            Font countFont = new Font("Arial", 14, FontStyle.Bold); // Font used for counts on potions and currency
            Brush countBrush = new SolidBrush(Color.White);


            // =========================
            //      HEALTH BAR
            // =========================
            int healthX = 20;
            int healthY = 20;

            int scaledHealthBarWidth = hBar.Width * scale;
            int scaledHealthBarHeight = hBar.Height * scale;
            int scaledHealthGraphicWidth = hGraphic.Width * scale;
            int scaledHealthGraphicHeight = hGraphic.Height * scale;

            if (healthPercent > 0)
            {
                int currentHealthWidth = (int)(scaledHealthBarWidth * healthPercent);
                Rectangle destRect = new Rectangle(healthX, healthY, currentHealthWidth, scaledHealthBarHeight);
                Rectangle srcRect = new Rectangle(0, 0, (int)(hBar.Width * healthPercent), hBar.Height);
                g.DrawImage(hBar, destRect, srcRect, GraphicsUnit.Pixel);
            }

            g.DrawImage(hGraphic, new Rectangle(healthX, healthY, scaledHealthGraphicWidth, scaledHealthGraphicHeight));



            // =========================
            //      STAMINA BAR
            // =========================
            int staminaX = 20;
            int staminaY = 80;

            int scaledStaminaBarWidth = sBar.Width * scale;
            int scaledStaminaBarHeight = sBar.Height * scale;
            int scaledStaminaGraphicWidth = sGraphic.Width * scale;
            int scaledStaminaGraphicHeight = sGraphic.Height * scale;

            if (staminaPercent > 0)
            {
                int currentStaminaWidth = (int)(scaledStaminaBarWidth * staminaPercent);
                Rectangle destRectStamina = new Rectangle(staminaX, staminaY, currentStaminaWidth, scaledStaminaBarHeight);
                Rectangle srcRectStamina = new Rectangle(0, 0, (int)(sBar.Width * staminaPercent), sBar.Height);
                g.DrawImage(sBar, destRectStamina, srcRectStamina, GraphicsUnit.Pixel);
            }

            g.DrawImage(sGraphic, new Rectangle(staminaX, staminaY, scaledStaminaGraphicWidth, scaledStaminaGraphicHeight));



            // =========================
            //    INVENTORY BAR BASE
            // =========================
            int inventoryX = InventoryMargin;
            int inventoryY = screenHeight - InventoryDrawH - InventoryMargin;

            g.DrawImage(inventorySlots, new Rectangle(inventoryX, inventoryY, InventoryDrawW, InventoryDrawH));



            // =========================
            //    INDIVIDUAL ICON POSITIONS
            // =========================
            float[] iconXs = new float[]
            {
                inventoryX + Slot1X,
                inventoryX + Slot2X,
                inventoryX + Slot3X,
                inventoryX + Slot4X,
                inventoryX + Slot5X,
                inventoryX + Slot6X,
                inventoryX + Slot7X
            };

            float[] iconYs = new float[]
            {
                inventoryY + Slot1Y,
                inventoryY + Slot2Y,
                inventoryY + Slot3Y,
                inventoryY + Slot4Y,
                inventoryY + Slot5Y,
                inventoryY + Slot6Y,
                inventoryY + Slot7Y
            };

            Image[] icons = new Image[]
            {
                waterScrollIcon,
                fireScrollIcon,
                earthScrollIcon,
                airScrollIcon,
                swordIcon,
                potionIcon,
                currencyIcon
            };

            for (int i = 0; i < SlotCount + 1; i++) // + 1 is the 7th slot unable to be selected
            {
                if (icons[i] == null) continue;
                g.DrawImage(icons[i], new RectangleF(iconXs[i], iconYs[i], ItemIconSize, ItemIconSize));
            }

            // Draw potion count on top of potion icon (slot 6, index 5)
            if (potionIcon != null && PotionCount >= 0)
            {
                float potionIconX = iconXs[5];
                float potionIconY = iconYs[5];
                
                string countText = PotionCount.ToString();
                SizeF textSize = g.MeasureString(countText, countFont);
                float textX = potionIconX + ItemIconSize - textSize.Width;
                float textY = potionIconY + ItemIconSize - textSize.Height;
                g.DrawString(countText, countFont, countBrush, textX, textY);
                
            }
            // Draw currency count on top of Currency icon (slot 7, index 6)
            if (currencyIcon != null && CurrencyCount >= 0)
            {
                float currencyIconX = iconXs[6];
                float currencyIconY = iconYs[6];

                string countText = CurrencyCount.ToString();
                SizeF textSize = g.MeasureString(countText, countFont);
                float textX = currencyIconX + ItemIconSize - textSize.Width;
                float textY = currencyIconY + ItemIconSize - textSize.Height;
                g.DrawString(countText, countFont, countBrush, textX, textY);
                
            }

            // =========================
            //    HIGHLIGHT BOX
            // =========================
            highlightTargetX = iconXs[selectedSlot] + (ItemIconSize / 2f) - (HighlightBoxWidth / 2f);
            highlightTargetY = iconYs[selectedSlot] + (ItemIconSize / 2f) - (HighlightBoxHeight / 2f);

            if (!highlightInitialized)
            {
                highlightCurrentX = highlightTargetX;
                highlightCurrentY = highlightTargetY;
                highlightInitialized = true;
            }

            if (Math.Abs(highlightCurrentX - highlightTargetX) > 0.5f)
            {
                float dir = highlightTargetX > highlightCurrentX ? 1f : -1f;
                highlightCurrentX += dir * HighlightSlideSpeed;
                if (dir > 0 && highlightCurrentX > highlightTargetX) highlightCurrentX = highlightTargetX;
                if (dir < 0 && highlightCurrentX < highlightTargetX) highlightCurrentX = highlightTargetX;
            }
            else
            {
                highlightCurrentX = highlightTargetX;
            }

            highlightCurrentY = highlightTargetY;

            using (Pen highlightPen = new Pen(Color.Yellow, HighlightThickness))
            {
                g.DrawRectangle(highlightPen, highlightCurrentX, highlightCurrentY, HighlightBoxWidth, HighlightBoxHeight);
            }



            // =========================
            //      ACTIVE SCROLL
            // =========================
            if (currentScrollSheet != null)
            {
                int scrollX = screenWidth - ScrollDrawSize - ScrollMargin;
                int scrollY = screenHeight - ScrollDrawSize - ScrollMargin;

                Rectangle srcRect = new Rectangle(scrollFrameIndex * ScrollFrameSize, 0, ScrollFrameSize, ScrollFrameSize);
                Rectangle destRect = new Rectangle(scrollX, scrollY, ScrollDrawSize, ScrollDrawSize);

                g.DrawImage(currentScrollSheet, destRect, srcRect, GraphicsUnit.Pixel);
            }
        }
    }
}