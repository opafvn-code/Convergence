using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace FadePlanet
{
    public enum BossPhase { Air, Water, Earth, Fire, Final }

    internal class Boss : Enemy
    {
        #region Boss Stats
        private const int BossMaxHealth = 2000;
        private const int EnemiesPerPhase = 10;
        private int EnemiesDefeatedInPhase = 0;

        // Phase frame ranges (0-indexed)
        // Air: frames 0-2 (1-3 in user's description)
        // Water: frames 4-10 (5-11 in user's description)
        // Earth: frames 10-14 (11-15 in user's description)
        // Fire: frames 14-18 (15-19 in user's description)
        // Final: frames 14-18 (15-19 in user's description)
        private static readonly (int start, int end)[] PhaseFrameRanges = new (int, int)[]
        {
            (0, 2),    // Air
            (4, 10),   // Water
            (10, 14),  // Earth
            (14, 18),  // Fire
            (14, 18)   // Final
        };

        // Spritesheet: 1960x2450, 4 rows of 4 frames, then 1 row of 3 frames
        // Frame size: 490x490 (1960/4 = 490, 2450/5 = 490)
        private const int FrameWidth = 490;
        private const int FrameHeight = 490;
        private const int FramesPerRow = 4;
        #endregion

        #region State
        public BossPhase CurrentPhase { get; private set; } = BossPhase.Air;
        private Bitmap bossSheet;
        private int animFrame = 0;
        private int animTimer = 0;
        private const int AnimFrameDuration = 8;
        private bool animForward = true;

        // Attack timers
        private int attackTimer = 0;
        private const int AttackInterval = 2000; // 2 seconds between attacks
        private int stunTimer = 0;
        #endregion

        #region Health Bar Graphics
        private Image healthBarGraphic;
        private Image healthBarFill;
        // Image dimensions: 320x55 pixels
        private const int HealthBarWidth = 320;
        private const int HealthBarHeight = 55;
        #endregion

        public Boss(Point pos, Size size, ObjectType type = ObjectType.Boss) : base(pos, size, EnemyType.Air)
        {
            Type = type; // Ensure type is Boss even though we call Enemy constructor
            SetMaxHealth(BossMaxHealth);
            // Upscale boss 2x for Cuphead-style appearance
            ObjSize = new Size(size.Width * 1, size.Height * 1);
            LoadImages();
        }

        private string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\"));
        }

        private void LoadImages()
        {
            try
            {
                string basePath = GetProjectRoot();
                bossSheet = new Bitmap(Path.Combine(basePath, @"Graphics\Enemies\Boss\BossStatue.png"));
                healthBarGraphic = Image.FromFile(Path.Combine(basePath, @"Graphics\UI\BossHealthGraphic.png"));
                healthBarFill = Image.FromFile(Path.Combine(basePath, @"Graphics\UI\BossHealthBar.png"));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load boss images: " + ex.Message);
            }
        }

        public override void Update(Player player)
        {
            // Boss is stationary - no movement from Enemy.Update

            // Update animation
            UpdateAnimation();

            // Handle phase progression
            if (EnemiesDefeatedInPhase >= EnemiesPerPhase && CurrentPhase != BossPhase.Final)
            {
                AdvanceToNextPhase();
            }

            // Handle attacks based on phase
            if (CurrentPhase == BossPhase.Final)
            {
                HandleFinalPhaseAttacks(player);
            }
            else
            {
                HandlePhaseEnemySpawning();
            }

            // Stun immunity - stunTimer logic removed
        }

        private void UpdateAnimation()
        {
            animTimer++;
            if (animTimer >= AnimFrameDuration)
            {
                animTimer = 0;

                var (startFrame, endFrame) = PhaseFrameRanges[(int)CurrentPhase];

                if (animForward)
                {
                    animFrame++;
                    if (animFrame >= endFrame)
                    {
                        animFrame = endFrame;
                        animForward = false;
                    }
                }
                else
                {
                    animFrame--;
                    if (animFrame <= startFrame)
                    {
                        animFrame = startFrame;
                        animForward = true;
                    }
                }
            }
        }

        private void AdvanceToNextPhase()
        {
            EnemiesDefeatedInPhase = 0;
            animFrame = PhaseFrameRanges[(int)CurrentPhase].start;
            animForward = true;

            switch (CurrentPhase)
            {
                case BossPhase.Air:
                    CurrentPhase = BossPhase.Water;
                    break;
                case BossPhase.Water:
                    CurrentPhase = BossPhase.Earth;
                    break;
                case BossPhase.Earth:
                    CurrentPhase = BossPhase.Fire;
                    break;
                case BossPhase.Fire:
                    CurrentPhase = BossPhase.Final;
                    break;
            }
        }

        private void HandlePhaseEnemySpawning()
        {
            // Spawn enemies for current phase
            int enemiesAlive = GameManager.GetObjectsByType(ObjectType.Enemy).Count;
            int enemiesToSpawn = Math.Min(2, EnemiesPerPhase - EnemiesDefeatedInPhase);

            if (enemiesToSpawn > 0 && enemiesAlive < 3)
            {
                EnemyType enemyType = EnemyType.Air;

                switch (CurrentPhase)
                {
                    case BossPhase.Air:
                        enemyType = EnemyType.Air;
                        break;
                    case BossPhase.Water:
                        enemyType = EnemyType.Water;
                        break;
                    case BossPhase.Earth:
                        enemyType = EnemyType.Earth;
                        break;
                    case BossPhase.Fire:
                        enemyType = EnemyType.Fire;
                        break;
                }

                Random rand = new Random();
                // Spawn enemies further right (closer to player's side, away from boss)
                int x = rand.Next(700, 1100);
                int y = rand.Next(150, 550);
                Point spawnPos = new Point(x, y);

                Enemy enemy = new Enemy(spawnPos, new Size((int)(32 * 3.0f), (int)(32 * 3.0f)), enemyType);
            }
        }

        private void HandleFinalPhaseAttacks(Player player)
        {
            attackTimer += 16;

            if (attackTimer >= AttackInterval)
            {
                attackTimer = 0;

                // Randomly choose between water ripple stun or fireball
                Random rand = new Random();
                int attackChoice = rand.Next(2);

                if (attackChoice == 0)
                {
                    // Water ripple stun
                    SpawnWaterRipple(player);
                }
                else
                {
                    // Fireball
                    SpawnFireball(player);
                }
            }
        }

        private void SpawnWaterRipple(Player player)
        {
            // Create a ripple at player's position to stun them
            PointF ripplePos = new PointF(player.Position.X + 112f - 32f, player.Position.Y + 112f - 32f);
            new Ripple(ripplePos);
        }

        private void SpawnFireball(Player player)
        {
            // Spawn fireball aimed at player
            PointF bossCenter = new PointF(Position.X + ObjSize.Width / 2f, Position.Y + ObjSize.Height / 2f);
            PointF playerCenter = new PointF(player.Position.X + 112f, player.Position.Y + 112f);

            float dx = playerCenter.X - bossCenter.X;
            float dy = playerCenter.Y - bossCenter.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);

            PointF direction = len > 0 ? new PointF(dx / len, dy / len) : new PointF(1f, 0f);

            PointF fireballPos = new PointF(bossCenter.X - 16f, bossCenter.Y - 16f);
            new Projectile(fireballPos, new Size(32, 32), direction, 8f, ElementType.Fire, 30);
        }

        public void OnEnemyDefeated()
        {
            if (CurrentPhase != BossPhase.Final)
            {
                EnemiesDefeatedInPhase++;
                // Boss takes damage when enemies die (but less than direct damage)
                TakeDamage(20);
            }
        }

        public override void TakeDamage(int damage, PointF sourcePosition)
        {
            if (CurrentPhase != BossPhase.Final)
                return; // Can only damage boss directly in final phase

            base.TakeDamage(damage); // Use WorldObject.TakeDamage via base.base? No, base.TakeDamage in Enemy calls base.TakeDamage in WorldObject.
        }

        public override void TakeDamage(int damage, PointF sourcePosition, float knockbackDistance)
        {
            if (CurrentPhase != BossPhase.Final)
                return;

            base.TakeDamage(damage);
        }

        public override void ApplyStun(int durationMs)
        {
            // Boss is immune to stun
        }

        public override void OnDeath()
        {
            GameManager.CurPlayer.AddCurrency(50);
            GameManager.TriggerVictory();
            GameManager.DespawnObject(this);
        }

        public override void Draw(Graphics g)
        {
            if (bossSheet == null) return;

            // Calculate source rectangle from spritesheet
            var (startFrame, endFrame) = PhaseFrameRanges[(int)CurrentPhase];
            int frameIndex = animFrame;

            // Calculate row and column
            int row = frameIndex / FramesPerRow;
            int col = frameIndex % FramesPerRow;

            int srcX = col * FrameWidth;
            int srcY = row * FrameHeight;

            Rectangle srcRect = new Rectangle(srcX, srcY, FrameWidth, FrameHeight);
            RectangleF destRect = new RectangleF(Position.X, Position.Y, ObjSize.Width, ObjSize.Height);

            g.DrawImage(bossSheet, destRect, srcRect, GraphicsUnit.Pixel);

            // Draw health bar
            DrawHealthBar(g);
        }

        private void DrawHealthBar(Graphics g)
        {
            if (healthBarGraphic == null || healthBarFill == null) return;

            // Calculate bar position (centered above boss)
            float barX = Position.X + (ObjSize.Width / 2f) - (healthBarGraphic.Width / 2f);
            float barY = Position.Y - healthBarGraphic.Height - 20;

            // Calculate health percentage
            float healthPercent = (float)Health / MaxHealth;

            // Draw the health fill first (behind the frame) using clipping
            if (healthPercent > 0)
            {
                int currentFillWidth = (int)(healthBarGraphic.Width * healthPercent);
                RectangleF fillDestRect = new RectangleF(barX, barY, currentFillWidth, healthBarGraphic.Height);
                RectangleF fillSrcRect = new RectangleF(0, 0, healthBarFill.Width * healthPercent, healthBarFill.Height);
                g.DrawImage(healthBarFill, fillDestRect, fillSrcRect, GraphicsUnit.Pixel);
            }

            // Draw the frame graphic on top
            g.DrawImage(healthBarGraphic, barX, barY, healthBarGraphic.Width, healthBarGraphic.Height);
        }
    }
}
