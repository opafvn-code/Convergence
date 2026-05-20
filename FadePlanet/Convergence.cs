using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static FadePlanet.GameManager;

namespace FadePlanet
{
    public partial class Convergence : Form
    {
        // --- DECLARE UI ---
        private UI gameUI;

        // UI Images
        private Image healthGraphic;
        private Image healthBar;
        private Image staminaGraphic;
        private Image staminaBar;
        private Image inventorySlots;

        private System.Windows.Forms.Timer gameLoop;

        private string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\"));
        }

        public Convergence()
        {
            InitializeComponent();

            this.ClientSize = GameManager.WindowSize;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += Convergence_KeyDown;
            this.KeyUp += Convergence_KeyUp;
            this.MouseClick += Convergence_MouseClick;

            // --- LOAD IMAGES ---
            string basePath = GetProjectRoot();
            

            try
            {
                healthGraphic = Image.FromFile(Path.Combine(basePath, @"Graphics\UI\HealthGraphic.png"));
                healthBar = Image.FromFile(Path.Combine(basePath, @"Graphics\UI\HealthBar.png"));
                staminaGraphic = Image.FromFile(Path.Combine(basePath, @"Graphics\UI\StaminaGraphic.png"));
                staminaBar = Image.FromFile(Path.Combine(basePath, @"Graphics\UI\StaminaBar.png"));
                inventorySlots = Image.FromFile(Path.Combine(basePath, @"Graphics\UI\Inventory Slots.png"));

                foreach (WorldObject e in GetObjectsByType(ObjectType.Enemy))
                {
                    if (e is Enemy en) en.LoadImages();
                    // Boss loads its own images in constructor
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Image load failed: " + ex.Message);
            }
            
            gameUI = new UI(basePath);

            // --- START GAME LOOP ---
            gameLoop = new System.Windows.Forms.Timer();
            gameLoop.Interval = 16;
            gameLoop.Tick += GameLoop_Tick;
            gameLoop.Start();
        }

        

        // --- INPUT HANDLING ---
        private void Convergence_KeyDown(object sender, KeyEventArgs e)
        {
            if (!CurPlayer.ScrollSwitchLocked)
            {
                var previousElement = CurPlayer.CurrentElement;
                CurPlayer.HandleScrollSwitch(e.KeyCode);

                if (CurPlayer.PendingElement != null && CurPlayer.CurrentElement == previousElement)
                    gameUI.StartScrollSwitch(CurPlayer.PendingElement.Type);
            }

            if (e.KeyCode == Keys.D1) gameUI.SetSelectedSlot(1, CurPlayer.ScrollSwitchLocked);
            if (e.KeyCode == Keys.D2) gameUI.SetSelectedSlot(2, CurPlayer.ScrollSwitchLocked);
            if (e.KeyCode == Keys.D3) gameUI.SetSelectedSlot(3, CurPlayer.ScrollSwitchLocked);
            if (e.KeyCode == Keys.D4) gameUI.SetSelectedSlot(4, CurPlayer.ScrollSwitchLocked);
            if (e.KeyCode == Keys.D5) gameUI.SetSelectedSlot(5, false);
            if (e.KeyCode == Keys.D6) gameUI.SetSelectedSlot(6, false);

            CurPlayer.HandleKeyDown(e);
        }
        private void Convergence_KeyUp(object sender, KeyEventArgs e)
        {
            CurPlayer.HandleKeyUp(e);
        }
        private void Convergence_Load(object sender, EventArgs e) 
        {
            GameManager.LoadRoom_One();
        }
        private void Convergence_MouseClick(object sender, MouseEventArgs e)
        {
            CurPlayer.HandleMouseClick(e, gameUI.SelectedSlot);
        }


        // --- GAME LOOP UPDATE ---
        private void GameLoop_Tick(object sender, EventArgs e)
        {
            // 1. Try spawn enemies for current realm
            GameManager.TrySpawnEnemies();

            // 2. Update victory fade
            GameManager.UpdateVictoryFade();

            // 3. Update player
            UpdateObjectType(ObjectType.Player, (obj) =>
            {
                if (obj is Player p)
                {
                    p.Update(GetObjectsByType(ObjectType.Enemy));
                }
            });

            // 3. Scroll animation milestones
            var (closingDone, openingDone) = gameUI.UpdateScrollAnimation();

            if (closingDone) CurPlayer.ConfirmScrollSwitch();
            if (openingDone)
            {
                CurPlayer.UnlockScrollSwitch();
                gameUI.FlushPendingSlot();
            }

            // 4. Remove dead enemies then update living ones
            GetObjectsByType(ObjectType.Enemy).RemoveAll(en =>
            {
                if (GameManager.AllObjects.TryGetValue(ObjectType.Enemy, out var dict))
                    return !dict.ContainsKey(en.Id);
                return true;
            });

            foreach (WorldObject obj in GetObjectsByType(ObjectType.Enemy))
            {
                if (obj is Boss boss)
                {
                    boss.Update(CurPlayer);
                }
                else if (obj is Enemy en)
                {
                    en.Update(CurPlayer);
                }
            }

            // 5. Update all projectiles
            UpdateObjectType(ObjectType.Projectile, (obj) =>
            {
                if (obj is Projectile proj)
                {
                    proj.Update();
                    // Remove projectiles that go off screen
                    if (proj.Position.X < -50 || proj.Position.X > ClientSize.Width + 50)
                    {
                        DespawnObject(proj);
                    }
                }
            });

            // 6. Update all ripples
            UpdateObjectType(ObjectType.None, (obj) =>
            {
                if (obj is Ripple r)
                {
                    r.Update();
                }
            });

            // 7. Update all items (tokens, potions, etc.)
            UpdateObjectType(ObjectType.Item, (obj) =>
            {
                if (obj is Item item)
                {
                    item.Update();

                    // Check for pickup
                    if (!CurPlayer.IsPlayingPickup && !CurPlayer.IsPlayingSlash)
                    {
                        float dx = (item.Position.X + Item.DrawSize / 2f) - (CurPlayer.Position.X + 112f);
                        float dy = (item.Position.Y + Item.DrawSize / 2f) - (CurPlayer.Position.Y + 112f);
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (distance <= Item.PickupRange)
                        {
                            CurPlayer.PickUpItem(item);
                            GameManager.DespawnObject(item);

                            if (item.ItemType == ItemType.Token)
                                CurPlayer.TriggerPickupAnimation();
                        }
                    }
                }
            });
            UpdateObjectType(ObjectType.Friendly, (obj) =>
            {
                if (obj is OldMan man)
                {
                    //Check interaction if player isn't attacking or picking something up
                    if (!CurPlayer.IsPlayingPickup && !CurPlayer.IsPlayingSlash)
                    {
                        float dx = (man.Position.X + man.ObjSize.Width / 2f) - (CurPlayer.Position.X + 112f);
                        float dy = (man.Position.Y + man.ObjSize.Height / 2f) - (CurPlayer.Position.Y + 112f);
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (distance <= OldMan.InteractDistance)
                        {
                            if (!man.IsInteracting && !man.HasInteracted)
                            {
                                man.HasInteracted = true;
                                man.OnInteract(CurPlayer);
                            }
                        }
                        else
                        {
                            //Player is outside radius so now the pop up can
                            //reopen if they choose to return to Old man
                            man.HasInteracted = false;
                        }
                    }
                }
            });

            // 8. Sync UI
            if (GameManager.CurPlayer != null)
            {
                gameUI.UpdateHealth(CurPlayer.Health, CurPlayer.MaxHealth);
                gameUI.UpdateStamina(CurPlayer.Stamina, CurPlayer.MaxStamina);
                gameUI.UpdatePotionCount(CurPlayer.Potions);
                gameUI.UpdateCurrency(CurPlayer.Currency);

            }
            

            // 9. Redraw
            this.Invalidate();
        }


        // --- DRAW TO THE SCREEN ---
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw all items
            DrawObjectType(e.Graphics, ObjectType.Item);

            // Draw all enemies
            foreach (WorldObject en in GetObjectsByType(ObjectType.Enemy))
                en.Draw(e.Graphics);

            // Draw all projectiles
            DrawObjectType(e.Graphics, ObjectType.Projectile);

            // Draw all ripples
            DrawObjectType(e.Graphics, ObjectType.None, (obj) => obj is Ripple);


            // Draw player and hitbox
            CurPlayer?.Draw(e.Graphics);
            CurPlayer?.DrawHitbox(e.Graphics);


            DrawObjectType(e.Graphics, ObjectType.Friendly, (obj) => obj is OldMan);

            // Draw UI
            if (healthGraphic != null && healthBar != null && staminaGraphic != null && staminaBar != null && inventorySlots != null)
            {
                gameUI.DrawWinFormsUI(
                    e.Graphics,
                    healthGraphic,
                    healthBar,
                    staminaGraphic,
                    staminaBar,
                    inventorySlots,
                    this.ClientSize.Width,
                    this.ClientSize.Height
                );
            }

            // Draw victory screen
            if (GameManager.GameWon)
            {
                DrawVictoryScreen(e.Graphics);
            }

        }

        private void DrawVictoryScreen(Graphics g)
        {
            // Draw semi-transparent overlay
            int alpha = (int)(GameManager.VictoryFadeAlpha * 255);
            using (SolidBrush overlayBrush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
            {
                g.FillRectangle(overlayBrush, 0, 0, ClientSize.Width, ClientSize.Height);
            }

            // Draw victory text when fade is complete
            if (GameManager.VictoryFadeAlpha >= 1f)
            {
                using (Font titleFont = new Font("Arial", 48, FontStyle.Bold))
                using (Font messageFont = new Font("Arial", 24))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    string title = "VICTORY!";
                    string message = "You have restored life to the world!";

                    SizeF titleSize = g.MeasureString(title, titleFont);
                    SizeF messageSize = g.MeasureString(message, messageFont);

                    float titleX = (ClientSize.Width - titleSize.Width) / 2;
                    float titleY = ClientSize.Height / 2 - 50;
                    float messageX = (ClientSize.Width - messageSize.Width) / 2;
                    float messageY = titleY + titleSize.Height + 30;

                    g.DrawString(title, titleFont, textBrush, titleX, titleY);
                    g.DrawString(message, messageFont, textBrush, messageX, messageY);
                }
            }
        }
    }
}