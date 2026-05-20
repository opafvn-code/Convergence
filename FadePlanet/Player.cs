using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static FadePlanet.Abilities;

namespace FadePlanet
{
    public class Player : WorldObject
    {
        // =====================================================================
        // PLAYER KNOCKBACK SETTINGS
        // =====================================================================
        private const float PlayerKnockbackDistance = 60f;
        private const float PlayerKnockbackSpeed = 5f;

        // =====================================================================
        // SWORD ATTACK HITBOX SETTINGS
        // =====================================================================
        private const float SwordHitboxWidth = 140f;
        private const float SwordHitboxHeight = 100f;
        private const float SwordHitboxOffsetY = 80f;
        private const int SwordDamage = 20;
        private const int RockBarrierDamage = 40;
        private const float RockBarrierKnockbackDistance = 130f;
        private const float BarrierHitboxWidth = 180f;
        private const int HealAmount = 40;
        // =====================================================================
        private const float StaminaRegenPerSecond = 10f;
        private const float GameLoopDeltaSeconds = 0.016f; // matches 16ms timer in Convergence
        // =====================================================================

        public const int InventorySlotEarthScroll = 2;
        public const int InventorySlotSword = 4;
        public const int InventorySlotPotion = 5;

        #region Player Stats
        public float Stamina { get; private set; } = 100f;
        public float MaxStamina { get; private set; } = 100f;

        
        #endregion

        #region Player Inventory
        public HashSet<ElementType> Tokens { get; set; } = new HashSet<ElementType>();
        public int Potions { get; private set; } = 3;
        public int Currency { get; private set; } = 30;
        public void AddCurrency(int amount) { Currency += amount; }
        public void AddPotions(int amount) { Potions += amount; }
        // Store token item to advance realm after animation completes
        private Item pendingTokenItem = null;

        public void PickUpItem(Item item)
        {
            switch (item.ItemType)
            {
                case ItemType.Token:
                    AddToken(item.TokenType);
                    // Store token and trigger animation, advance realm after animation
                    pendingTokenItem = item;
                    TriggerPickupAnimation();
                    break;
                case ItemType.Potion:
                    Potions++;
                    break;
            }
        }
        public void AddToken(ElementType element)
        {
            if (element == ElementType.None) return;

            bool wasAdded = Tokens.Add(element);

            if (wasAdded)
            {
                Console.WriteLine($"New token acquired: {element}!");
            }
        }

        public bool HasToken(ElementType element) { return Tokens.Contains(element); }

        #endregion

        #region Primary Attack Cooldown
        private const float GameLoopDeltaMs = 16f; // 16ms per frame matches timer interval
        #endregion

        #region Player Abilities
        public IElement CurrentElement { get; private set; }
        public IElement PendingElement { get; private set; }

        private Dictionary<Keys, IElement> _abilities = new Dictionary<Keys, IElement>
        {
            { Keys.D1, new WaterScroll() },
            { Keys.D2, new FireScroll() },
            { Keys.D3, new EarthScroll() },
            { Keys.D4, new AirScroll() }
        };

        public bool ScrollSwitchLocked { get; set; } = false;
        #endregion

        #region Hitbox
        // =====================================================================
        // HITBOX SETTINGS
        // =====================================================================
        private const float HitboxWidth = 80f;
        private const float HitboxHeight = 100f;
        private const float HitboxOffsetX = 72f;
        private const float HitboxOffsetY = 110f;

        // Override the base WorldObject Hitbox with a more precise player hitbox
        public override RectangleF Hitbox => new RectangleF(
            Position.X + HitboxOffsetX,
            Position.Y + HitboxOffsetY,
            HitboxWidth,
            HitboxHeight
        );

        public RectangleF SwordHitbox => GetMeleeHitbox(SwordHitboxWidth);

        public RectangleF BarrierHitbox => GetMeleeHitbox(BarrierHitboxWidth);

        private RectangleF GetMeleeHitbox(float width)
        {
            const float meleeGap = 8f;
            PointF dir = GetAttackDirection();

            // Calculate hitbox position based on attack direction
            float offsetXLocal, offsetYLocal;
            float hitboxWidth, hitboxHeight;

            if (dir.Y < 0) // Upward attack
            {
                offsetXLocal = HitboxOffsetX + (HitboxWidth - width) / 2;
                offsetYLocal = HitboxOffsetY - meleeGap - SwordHitboxHeight;
                hitboxWidth = width;
                hitboxHeight = SwordHitboxHeight;
            }
            else if (dir.Y > 0) // Downward attack
            {
                offsetXLocal = HitboxOffsetX + (HitboxWidth - width) / 2;
                offsetYLocal = HitboxOffsetY + HitboxHeight + meleeGap;
                hitboxWidth = width;
                hitboxHeight = SwordHitboxHeight;
            }
            else if (dir.X < 0) // Left attack
            {
                offsetXLocal = HitboxOffsetX - meleeGap - width;
                offsetYLocal = SwordHitboxOffsetY;
                hitboxWidth = width;
                hitboxHeight = SwordHitboxHeight;
            }
            else // Right attack (default)
            {
                offsetXLocal = HitboxOffsetX + HitboxWidth + meleeGap;
                offsetYLocal = SwordHitboxOffsetY;
                hitboxWidth = width;
                hitboxHeight = SwordHitboxHeight;
            }

            return new RectangleF(
                Position.X + offsetXLocal,
                Position.Y + offsetYLocal,
                hitboxWidth,
                hitboxHeight
            );
        }

        // Override the base ShowHitbox toggle
        public override bool ShowHitbox { get; set; } = false;
        #endregion

        #region Knockback
        private bool isKnockedBack = false;
        private PointF knockbackDirection;
        private float knockbackRemaining = 0f;
        #endregion


        public Player(Point pos, Size size) : base(pos, size, ObjectType.Player)
        {
            MaxHealth = 100;
            Health = 100;
            CurrentElement = _abilities[Keys.D4];
            ObjSize = new Size(224, 224);
            LoadImages();
        }

        #region Movement & Animation
        public int Speed { get; set; } = 4;

        #region Images/Bitmaps
        private Image facingB;
        private Image facingF;
        private Image facingR;
        private Image facingL;
        private Image idleR1;
        private Image idleR2;
        private Image idleL1;
        private Image idleL2;
        private Image currentImage;

        private Image pickupFrame1;
        private Image pickupFrame2;
        private Image pickupFrame3;

        private Bitmap slashSheetR;
        private Bitmap slashSheetL;
        private Bitmap healSheetR;
        private Bitmap healSheetL;
        private Bitmap rockBarrierSheetR;
        private Bitmap rockBarrierSheetL;
        #endregion

        #region Token Pickup Anim
        public bool IsPlayingPickup { get; private set; } = false;
        private int pickupFrameIndex = 0;
        private int pickupFrameTimer = 0;
        private const int PickupFrameDuration = 12;
        #endregion

        #region Slash Anim
        public bool IsPlayingSlash { get; private set; } = false;
        private int slashFrameIndex = 0;
        private int slashFrameTimer = 0;
        private const int SlashFrameDuration = 2;
        private const int SlashTotalFrames = 6;

        public bool swordHitDealtThisSwing = false; // set when processing a swing


       
        public bool isFacingLeft { get; private set; } = false;

        private bool IsActionLocked => IsPlayingSlash || IsPlayingHeal || IsPlayingRockBarrier || IsPlayingPickup;

        private static readonly Point[] SlashFramePositions = new Point[]
        {
            new Point(0,   0),
            new Point(224, 0),
            new Point(0,   224),
            new Point(224, 224),
            new Point(0,   448),
            new Point(224, 448)
        };
        #endregion
        
        #region Rock Barrier Anim
        public bool IsPlayingRockBarrier { get; private set; } = false;
        private int rockBarrierFrameIndex = 0;
        private int rockBarrierFrameTimer = 0;
        private const int RockBarrierFrameDuration = 2;
        private const int RockBarrierTotalFrames = 11;

        public bool barrierHitDealtThisSwing = false;

        // 672x896 sheet, 224x224 frames in 3 columns (rows 1–3 full, row 4 has frames 10–11)
        private static readonly Point[] RockBarrierFramePositions = new Point[]
        {
            new Point(0,   0),    // 1
            new Point(224, 0),    // 2
            new Point(448, 0),    // 3
            new Point(0,   224),  // 4
            new Point(224, 224),  // 5
            new Point(448, 224),  // 6
            new Point(0,   448),  // 7
            new Point(224, 448),  // 8
            new Point(448, 448),  // 9
            new Point(0,   672),  // 10
            new Point(224, 672)   // 11
        };
        #endregion

        #region Health Gain Anim
        public bool IsPlayingHeal { get; private set; } = false;
        private int healFrameIndex = 0;
        private int healFrameTimer = 0;
        private const int HealFrameDuration = 3;
        private const int HealTotalFrames = 12;
        
        // 672x896 sheet, 224x224 frames in 3 columns (4 full rows, frames 1–12)
        private static readonly Point[] HealFramePositions = new Point[]
        {
            new Point(0,   0),    // 1
            new Point(224, 0),    // 2
            new Point(448, 0),    // 3
            new Point(0,   224),  // 4
            new Point(224, 224),  // 5
            new Point(448, 224),  // 6
            new Point(0,   448),  // 7
            new Point(224, 448),  // 8
            new Point(448, 448),  // 9
            new Point(0,   672),  // 10
            new Point(224, 672),  // 11
            new Point(448, 672)   // 12
        };
        #endregion

        private int frameCounter = 0;
        private int idleAnimationSpeed = 30;
        private bool isIdle1 = true;
        private bool CanMove = true;

        public void SetMovementState(bool canMove)
        {
            CanMove = canMove;

            if (!CanMove)
            {
                isMovingUp = false;
                isMovingDown = false;
                isMovingLeft = false;
                isMovingRight = false;
            }
        }
        private bool isMovingUp, isMovingDown, isMovingLeft, isMovingRight;

        private string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\"));
        }

        public void LoadImages()
        {
            try
            {
                string basePath = GetProjectRoot();

                facingB = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Walking\MainCharacter.FacingB.png"));
                facingF = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Walking\MainCharacter.FacingF.png"));
                facingR = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Walking\MainCharacter.FacingR.png"));
                facingL = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Walking\MainCharacter.FacingL.png"));

                idleR1 = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Walking\MainCharacter.Idle1.png"));
                idleR2 = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Walking\MainCharacter.Idle2.png"));

                idleL1 = (Image)idleR1.Clone();
                idleL1.RotateFlip(RotateFlipType.RotateNoneFlipX);
                idleL2 = (Image)idleR2.Clone();
                idleL2.RotateFlip(RotateFlipType.RotateNoneFlipX);

                pickupFrame1 = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Tokens\ClaimingTokens1.png"));
                pickupFrame2 = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Tokens\ClaimingTokens2.png"));
                pickupFrame3 = Image.FromFile(Path.Combine(basePath, @"Graphics\Player\Tokens\ClaimingTokens3.png"));

                slashSheetR = new Bitmap(Path.Combine(basePath, @"Graphics\Player\Sword Animation\Slashing.png"));
                slashSheetL = (Bitmap)slashSheetR.Clone();
                slashSheetL.RotateFlip(RotateFlipType.RotateNoneFlipX);

                healSheetR = new Bitmap(Path.Combine(basePath, @"Graphics\Player\Healing\Healing.png"));
                healSheetL = (Bitmap)healSheetR.Clone();
                healSheetL.RotateFlip(RotateFlipType.RotateNoneFlipX);

                rockBarrierSheetR = new Bitmap(Path.Combine(basePath, @"Graphics\Player\Attacks\MainCharacter.RockBarrier.png"));
                rockBarrierSheetL = (Bitmap)rockBarrierSheetR.Clone();
                rockBarrierSheetL.RotateFlip(RotateFlipType.RotateNoneFlipX);

                currentImage = idleR1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load player images! Error: " + ex.Message);
            }
        }

        #region Animation Triggers
        public void TriggerPickupAnimation()
        {
            IsPlayingPickup = true;
            pickupFrameIndex = 0;
            pickupFrameTimer = 0;
        }
        public void TriggerSlashAnimation()
        {
            if (IsActionLocked) return;

            IsPlayingSlash = true;
            slashFrameIndex = 0;
            slashFrameTimer = 0;
            swordHitDealtThisSwing = false;
        }
        public void TriggerHealAnimation()
        {
            if (IsActionLocked || Potions <= 0) return;

            IsPlayingHeal = true;
            healFrameIndex = 0;
            healFrameTimer = 0;
        }
        public void TriggerRockBarrierAnimation()
        {
            if (IsActionLocked) return;

            IsPlayingRockBarrier = true;
            rockBarrierFrameIndex = 0;
            rockBarrierFrameTimer = 0;
            barrierHitDealtThisSwing = false;
        }

        #endregion

        #endregion
        // Takes a plain list of WorldObjects so accessibility matches public
        public void Update(List<WorldObject> enemyObjects)
        {
            RegenerateStamina();

            // Update ability cooldowns
            CurrentElement?.UpdateCooldown(GameLoopDeltaMs);
            if (PendingElement != null && PendingElement != CurrentElement)
            {
                PendingElement.UpdateCooldown(GameLoopDeltaMs);
            }

            // Handle knockback first — overrides everything
            if (isKnockedBack)
            {
                if (knockbackRemaining > 0)
                {
                    float step = Math.Min(PlayerKnockbackSpeed, knockbackRemaining);
                    Position = new PointF(
                        Position.X + knockbackDirection.X * step,
                        Position.Y + knockbackDirection.Y * step
                    );
                    knockbackRemaining -= step;
                }
                else
                {
                    isKnockedBack = false;
                }

                currentImage = isFacingLeft ? idleL1 : idleR1;
                return;
            }

            // Pickup animation
            if (IsPlayingPickup)
            {
                pickupFrameTimer++;
                if (pickupFrameTimer >= PickupFrameDuration)
                {
                    pickupFrameTimer = 0;
                    pickupFrameIndex++;

                    if (pickupFrameIndex >= 3)
                    {
                        IsPlayingPickup = false;
                        pickupFrameIndex = 0;
                        currentImage = isFacingLeft ? idleL1 : idleR1;

                        // Advance to next realm after token animation completes
                        if (pendingTokenItem != null)
                        {
                            GameManager.OnTokenCollected();
                            pendingTokenItem = null;
                        }
                    }
                }

                if (pickupFrameIndex == 0) currentImage = pickupFrame1;
                else if (pickupFrameIndex == 1) currentImage = pickupFrame2;
                else if (pickupFrameIndex == 2) currentImage = pickupFrame3;

                return;
            }

            // Heal animation
            if (IsPlayingHeal)
            {
                healFrameTimer++;

                if (healFrameTimer >= HealFrameDuration)
                {
                    healFrameTimer = 0;
                    healFrameIndex++;

                    if (healFrameIndex >= HealTotalFrames)
                    {
                        IsPlayingHeal = false;
                        healFrameIndex = 0;
                        currentImage = isFacingLeft ? idleL1 : idleR1;
                        ApplyHeal();
                    }
                }

                return;
            }

            // Rock barrier animation
            if (IsPlayingRockBarrier)
            {
                rockBarrierFrameTimer++;

                if (rockBarrierFrameTimer >= RockBarrierFrameDuration)
                {
                    rockBarrierFrameTimer = 0;
                    rockBarrierFrameIndex++;

                    if (rockBarrierFrameIndex >= RockBarrierTotalFrames)
                    {
                        IsPlayingRockBarrier = false;
                        rockBarrierFrameIndex = 0;
                        currentImage = isFacingLeft ? idleL1 : idleR1;
                    }
                }

                if (!barrierHitDealtThisSwing && enemyObjects != null)
                {
                    PointF attackSource = new PointF(Position.X + 112f, Position.Y + 112f);

                    foreach (WorldObject obj in enemyObjects)
                    {
                        if (BarrierHitbox.IntersectsWith(obj.Bounds))
                        {
                            // Check if it's a boss - only damage in final phase
                            if (obj is Boss boss)
                            {
                                if (boss.CurrentPhase == BossPhase.Final)
                                {
                                    boss.TakeDamage(RockBarrierDamage);
                                }
                            }
                            else if (obj is Enemy enemy)
                            {
                                enemy.TakeDamage(RockBarrierDamage, attackSource, RockBarrierKnockbackDistance);
                            }
                        }
                    }
                    barrierHitDealtThisSwing = true;
                }

                return;
            }

            // Slash animation
            if (IsPlayingSlash)
            {
                slashFrameTimer++;

                if (slashFrameTimer >= SlashFrameDuration)
                {
                    slashFrameTimer = 0;
                    slashFrameIndex++;

                    if (slashFrameIndex >= SlashTotalFrames)
                    {
                        IsPlayingSlash = false;
                        slashFrameIndex = 0;
                        currentImage = isFacingLeft ? idleL1 : idleR1;
                    }
                }

                // Check sword hitbox against enemies once per swing
                if (!swordHitDealtThisSwing && enemyObjects != null)
                {
                    PointF attackSource = new PointF(Position.X + 112f, Position.Y + 112f);

                    foreach (WorldObject obj in enemyObjects)
                    {
                        if (SwordHitbox.IntersectsWith(obj.Bounds))
                        {
                            // Check if it's a boss - only damage in final phase
                            if (obj is Boss boss)
                            {
                                if (boss.CurrentPhase == BossPhase.Final)
                                {
                                    boss.TakeDamage(SwordDamage);
                                }
                            }
                            else if (obj is Enemy enemy)
                            {
                                enemy.TakeDamage(SwordDamage, attackSource);
                            }
                        }
                    }
                    swordHitDealtThisSwing = true;
                }

                return;
            }

            // Normal movement
            bool isMoving = false;
            float x = Position.X;
            float y = Position.Y;

            if (isMovingUp) { y -= Speed; isMoving = true; currentImage = facingB; }
            if (isMovingDown) { y += Speed; isMoving = true; currentImage = facingF; }
            if (isMovingLeft) { x -= Speed; isMoving = true; isFacingLeft = true; currentImage = facingL; }
            if (isMovingRight) { x += Speed; isMoving = true; isFacingLeft = false; currentImage = facingR; }

            RectangleF nextBounds = new RectangleF(x, y, HitboxWidth, HitboxHeight);

            // Window bounds restraints
            if (nextBounds.X < 0) x = 0;
            if (nextBounds.Y < 0) y = 0;
            if (nextBounds.X > GameManager.WindowSize.Width - Hitbox.Width) x = GameManager.WindowSize.Width - Hitbox.Width;
            if (nextBounds.Y > GameManager.WindowSize.Height - ObjSize.Height) y = GameManager.WindowSize.Height - ObjSize.Height;

            Position = new PointF(x, y);

            if (!isMoving)
            {
                frameCounter++;
                if (frameCounter >= idleAnimationSpeed)
                {
                    frameCounter = 0;
                    isIdle1 = !isIdle1;
                }
                currentImage = isFacingLeft
                    ? (isIdle1 ? idleL1 : idleL2)
                    : (isIdle1 ? idleR1 : idleR2);
            }
            else
            {
                frameCounter = 0;
            }
        }

        public void ApplyKnockback(PointF sourcePosition)
        {
            float kDx = Position.X - sourcePosition.X;

            // Only use X-axis knockback, no Y-axis
            knockbackDirection = kDx != 0
                ? new PointF(Math.Sign(kDx), 0f)
                : new PointF(1f, 0f);

            knockbackRemaining = PlayerKnockbackDistance;
            isKnockedBack = true;
        }

        public PointF GetAttackDirection()
        {
            // Check movement direction first (WASD keys)
            if (isMovingUp) return new PointF(0, -1);
            if (isMovingDown) return new PointF(0, 1);
            if (isMovingLeft) return new PointF(-1, 0);
            if (isMovingRight) return new PointF(1, 0);

            // Fall back to facing direction
            return isFacingLeft ? new PointF(-1, 0) : new PointF(1, 0);
        }

        public override void Draw(Graphics g)
        {
            if (IsPlayingHeal)
            {
                if (isFacingLeft)
                    DrawSpritesheetFrameFlipped(g, healSheetL, HealFramePositions, healFrameIndex, 448);
                else
                    DrawSpritesheetFrame(g, healSheetR, HealFramePositions, healFrameIndex, 448);
                return;
            }

            if (IsPlayingRockBarrier)
            {
                if (isFacingLeft)
                    DrawSpritesheetFrameFlipped(g, rockBarrierSheetL, RockBarrierFramePositions, rockBarrierFrameIndex, 448);
                else
                    DrawSpritesheetFrame(g, rockBarrierSheetR, RockBarrierFramePositions, rockBarrierFrameIndex, 448);
                return;
            }

            if (IsPlayingSlash)
            {
                if (isFacingLeft)
                    DrawSpritesheetFrameFlipped(g, slashSheetL, SlashFramePositions, slashFrameIndex, 224);
                else
                    DrawSpritesheetFrame(g, slashSheetR, SlashFramePositions, slashFrameIndex, 224);
                return;
            }

            if (currentImage != null)
                g.DrawImage(currentImage, Position.X, Position.Y, 224, 224);
        }

        private void DrawSpritesheetFrame(Graphics g, Bitmap sheet, Point[] framePositions, int frameIndex, int maxFrameX)
        {
            if (sheet == null) return;

            Point framePos = framePositions[frameIndex];
            int srcX = framePos.X;
            Rectangle srcRect = new Rectangle(srcX, framePos.Y, 224, 224);
            RectangleF destRect = new RectangleF(Position.X, Position.Y, 224, 224);
            g.DrawImage(sheet, destRect, srcRect, GraphicsUnit.Pixel);
        }

        private void DrawSpritesheetFrameFlipped(Graphics g, Bitmap sheet, Point[] framePositions, int frameIndex, int maxFrameX)
        {
            if (sheet == null) return;

            Point framePos = framePositions[frameIndex];
            // For flipped spritesheet, calculate mirrored X position
            int srcX = maxFrameX - framePos.X - 224;
            Rectangle srcRect = new Rectangle(srcX, framePos.Y, 224, 224);
            RectangleF destRect = new RectangleF(Position.X, Position.Y, 224, 224);
            g.DrawImage(sheet, destRect, srcRect, GraphicsUnit.Pixel);
        }

        #region Stat Functions
        private void ApplyHeal()
        {
            if (Potions <= 0) return;

            Potions--;
            Health = Math.Min(MaxHealth, Health + HealAmount);
        }

        private void RegenerateStamina()
        {
            if (Stamina >= MaxStamina) return;

            Stamina = Math.Min(MaxStamina, Stamina + StaminaRegenPerSecond * GameLoopDeltaSeconds);
        }

        public void UseStamina(float amount)
        {
            Stamina = Math.Max(0, Stamina - amount);
        }

        public bool CanUseAbility(float staminaCost)
        {
            return Stamina >= staminaCost && CurrentElement.CooldownRemaining <= 0f;
        }
        #endregion

        #region Hitbox Drawing
        public void DrawHitbox(Graphics g)
        {
            if (!ShowHitbox) return;

            using (Pen hitboxPen = new Pen(Color.Red, 2f))
                g.DrawRectangle(hitboxPen, Hitbox.X, Hitbox.Y, Hitbox.Width, Hitbox.Height);

            if (IsPlayingSlash)
            {
                using (Pen swordPen = new Pen(Color.Blue, 2f))
                    g.DrawRectangle(swordPen, SwordHitbox.X, SwordHitbox.Y, SwordHitbox.Width, SwordHitbox.Height);
            }

            if (IsPlayingRockBarrier)
            {
                using (Pen barrierPen = new Pen(Color.SaddleBrown, 2f))
                    g.DrawRectangle(barrierPen, BarrierHitbox.X, BarrierHitbox.Y, BarrierHitbox.Width, BarrierHitbox.Height);
            }
        }
        #endregion

        #region Input
        public void HandleKeyDown(KeyEventArgs e)
        {
            if (CanMove)
            {
                if (e.KeyCode == Keys.W) isMovingUp = true;
                if (e.KeyCode == Keys.S) isMovingDown = true;
                if (e.KeyCode == Keys.A) isMovingLeft = true;
                if (e.KeyCode == Keys.D) isMovingRight = true;
            }
            

            if (e.KeyCode == Keys.H) ShowHitbox = !ShowHitbox;
        }

        public void HandleKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.W) isMovingUp = false;
            if (e.KeyCode == Keys.S) isMovingDown = false;
            if (e.KeyCode == Keys.A) isMovingLeft = false;
            if (e.KeyCode == Keys.D) isMovingRight = false;
        }

        public void HandleMouseClick(MouseEventArgs e, int selectedSlot)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Slot 5: sword attacks only, no elemental attacks
                if (selectedSlot == 5)
                {
                    TriggerSlashAnimation();
                    return;
                }

                if (selectedSlot == InventorySlotPotion)
                {
                    ApplyHeal();
                    TriggerHealAnimation();
                    return;
                }

               
                // Water attack: freeze player, no animation, just idle frame
                if (selectedSlot == InventorySlotEarthScroll && CurrentElement is EarthScroll)
                {
                    TriggerRockBarrierAnimation();
                }
                else if (CurrentElement is WaterScroll)
                {
                    // Water attack: freeze player in place, show idle frame, no slash animation
                    SetMovementState(false);
                    currentImage = isFacingLeft ? idleL1 : idleR1;
                }
                else
                {
                    TriggerSlashAnimation();
                }

                //Only reset cooldown if attack is activated
                if (CurrentElement?.PrimaryAttack(this) == true)
                {
                    CurrentElement.CooldownRemaining = CurrentElement.CooldownMs;
                }
            }
            if (e.Button == MouseButtons.Right)
            {
                if (!IsActionLocked)
                {
                    TriggerSlashAnimation();

                }
            }
        }

        public void HandleScrollSwitch(Keys key)
        {
            if (ScrollSwitchLocked) return;

            if (_abilities.ContainsKey(key))
            {
                if (CurrentElement == _abilities[key])
                    Console.WriteLine("Already active");
                else
                {
                    PendingElement = _abilities[key];
                    ScrollSwitchLocked = true;
                    Console.WriteLine($"Switching to {PendingElement.Type}...");
                }
            }
        }

        public void ConfirmScrollSwitch()
        {
            if (PendingElement == null) return;
            CurrentElement = PendingElement;
            PendingElement = null;
            Console.WriteLine($"Switched to {CurrentElement.Type}!");
        }

        public void UnlockScrollSwitch()
        {
            ScrollSwitchLocked = false;
        }
        #endregion

        #region Damage/Death Functions
        public override void OnDeath()
        {
            // Reset game or handle player death
            MessageBox.Show("Game Over! You have fallen.");
            Application.Restart();
        }
        #endregion

        
    }
}