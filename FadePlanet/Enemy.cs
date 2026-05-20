using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace FadePlanet
{
    // Made public so Player.Update(List<Enemy>) accessibility matches
    public enum EnemyType { Air, Water, Earth, Fire }
    public enum EnemyState { Idle, Walking, Attacking, Knockback, Stunned }

    internal class Enemy : WorldObject
    {
        // =====================================================================
        // ENEMY STATS — Adjust these to change each enemy's feel
        // =====================================================================
        private const int AirHealth = 30;
        private const int WaterHealth = 50;
        private const int EarthHealth = 80;
        private const int FireHealth = 120;

        private const int AirDamage = 5;
        private const int WaterDamage = 10;
        private const int EarthDamage = 18;
        private const int FireDamage = 28;

        private const float AirWalkSpeed = 2.5f;
        private const float WaterWalkSpeed = 1.8f;
        private const float EarthWalkSpeed = 1.2f;
        private const float FireWalkSpeed = 2.0f;

        private const float DetectionRadius = 300f;
        private const float AttackRadius = 150f; // Increased from 60f to keep distance
        private const float StoppingDistance = 120f; // Distance to stop and prepare leap
        private const float PlayerSeparationBuffer = 6f; // Extra space kept from the player hitbox
        private const float HopHeight = 8f;
        private const float HopSpeed = 0.15f;
        private const float AttackJumpSpeed = 6f;
        private const float EnemyKnockbackDistance = 80f;
        private const float EnemyKnockbackSpeed = 5f;
        private const float DrawScale = 3.0f;
        private static readonly int DrawSize = (int)(32 * DrawScale);

        private const int HealthBarWidth = 60;
        private const int HealthBarHeight = 6;
        private const int HealthBarOffsetY = 10;
        // =====================================================================

        #region Stats
        public EnemyType EnemyType { get; private set; }
        public ElementType ElementType { get; private set; }
        public int Damage { get; protected set; }
        public float WalkSpeed { get; protected set; }
        #endregion

        #region State
        public EnemyState State { get; private set; } = EnemyState.Idle;
        private float hopOffset = 0f;
        private float hopTimer = 0f;
        private bool facingLeft = false;

        private PointF attackOrigin;
        private PointF attackTarget;
        private bool attackHitDealt = false;
        private bool returningToOrigin = false;
        private int leapPrepareTimer = 0;
        private const int LeapPrepareTime = 500; // 500ms wind-up before leap

        private PointF knockbackDirection;
        private float knockbackRemaining = 0f;

        private int stunRemaining = 0;
        #endregion

        #region Animation
        private Bitmap walkSheet;
        private Bitmap idleSheet;

        private int walkFrameCount;
        private int idleFrameCount;

        private int animFrame = 0;
        private int animTimer = 0;
        private const int AnimFrameDuration = 8;
        #endregion

        public Enemy(Point pos, Size size, EnemyType type) : base(pos, size, ObjectType.Enemy)
        {
            EnemyType = type;

            switch (type)
            {
                case EnemyType.Air:
                    ElementType = ElementType.Air;
                    MaxHealth = AirHealth;
                    Damage = AirDamage;
                    WalkSpeed = AirWalkSpeed;
                    walkFrameCount = 4;
                    idleFrameCount = 3;
                    break;
                case EnemyType.Water:
                    ElementType = ElementType.Water;
                    MaxHealth = WaterHealth;
                    Damage = WaterDamage;
                    WalkSpeed = WaterWalkSpeed;
                    walkFrameCount = 7;
                    idleFrameCount = 3;
                    break;
                case EnemyType.Earth:
                    ElementType = ElementType.Earth;
                    MaxHealth = EarthHealth;
                    Damage = EarthDamage;
                    WalkSpeed = EarthWalkSpeed;
                    walkFrameCount = 6;
                    idleFrameCount = 7;
                    break;
                case EnemyType.Fire:
                    ElementType = ElementType.Fire;
                    MaxHealth = FireHealth;
                    Damage = FireDamage;
                    WalkSpeed = FireWalkSpeed;
                    walkFrameCount = 7;
                    idleFrameCount = 5;
                    break;
            }

            Health = MaxHealth;

            LoadImages();
        }

        private string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\"));
        }

        public virtual void LoadImages()
        {
            try
            {
                string basePath = GetProjectRoot();
                string typeName = EnemyType.ToString();

                walkSheet = new Bitmap(Path.Combine(basePath, $@"Graphics\Enemies\Walk\{typeName}EnemyWalk.png"));
                idleSheet = new Bitmap(Path.Combine(basePath, $@"Graphics\Enemies\Idle\{typeName}EnemyIdle.png"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load {EnemyType} enemy images: " + ex.Message);
            }
        }

        // =====================================================================
        // UPDATE
        // =====================================================================
        public virtual void Update(Player player)
        {
            if (stunRemaining > 0)stun duration
            if (stunRemaining > 0)
            {
                stunRemaining -= 16; // ~16ms per frame at 60fps
            }

            float dx = player.Position.X + 112f - (Position.X + DrawSize / 2f);
            float dy = player.Position.Y + 112f - (Position.Y + DrawSize / 2f);
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            facingLeft = dx < 0;

            switch (State)
            {
                case EnemyState.Idle:
                    UpdateAnimation(idleFrameCount);
                    if (distance <= DetectionRadius)
                        State = EnemyState.Walking;
                    break;

                case EnemyState.Walking:
                    UpdateAnimation(walkFrameCount);
                    UpdateHop();

                    if (distance <= StoppingDistance)
                    {
                        // Close enough to attack - prepare leap
                        attackOrigin = Position;

                        // Calculate attack target as the edge of player's hitbox
                        float playerHitboxLeft = player.Position.X + 72f;
                        float playerHitboxRight = player.Position.X + 72f + 80f;
                        float playerHitboxCenterY = player.Position.Y + 110f + 50f;

                        // Set target to the edge of hitbox on the side enemy is approaching from
                        if (facingLeft)
                        {
                            // Enemy is to the right, target left edge of hitbox
                            attackTarget = new PointF(playerHitboxLeft, playerHitboxCenterY);
                        }
                        else
                        {
                            // Enemy is to the left, target right edge of hitbox
                            attackTarget = new PointF(playerHitboxRight, playerHitboxCenterY);
                        }

                        attackHitDealt = false;
                        returningToOrigin = false;
                        leapPrepareTimer = 0;
                        State = EnemyState.Attacking;
                        break;
                    }

                    // Once triggered, enemies don't stop following player
                    // Removed the check that would make them go back to idle when distance > DetectionRadius

                    float len = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (len > 0)
                    {
                        PointF nextPosition = new PointF(
                            Position.X + (dx / len) * WalkSpeed,
                            Position.Y + (dy / len) * WalkSpeed
                        );

                        if (!WouldOverlapPlayerHitbox(nextPosition, player))
                            Position = nextPosition;
                    }

                    break;

                case EnemyState.Attacking:
                    UpdateAnimation(walkFrameCount);

                    if (!returningToOrigin)
                    {
                        // Leap preparation phase
                        leapPrepareTimer += 16; // ~16ms per frame

                        if (leapPrepareTimer < LeapPrepareTime)
                        {
                            // During wind-up, stay at attack origin and don't move
                            // This gives a visible tell before the leap
                        }
                        else
                        {
                            // Leap phase - fly toward player
                            float atDx = attackTarget.X - Position.X;
                            float atDy = attackTarget.Y - Position.Y;
                            float atLen = (float)Math.Sqrt(atDx * atDx + atDy * atDy);

                            if (atLen > AttackJumpSpeed)
                            {
                                Position = new PointF(
                                    Position.X + (atDx / atLen) * AttackJumpSpeed,
                                    Position.Y + (atDy / atLen) * AttackJumpSpeed
                                );

                                if (!attackHitDealt && Bounds.IntersectsWith(player.Hitbox))
                                {
                                    player.TakeDamage(Damage);
                                    player.ApplyKnockback(Position);
                                    attackHitDealt = true;
                                }
                            }
                            else
                            {
                                // Reached target — start bouncing back
                                returningToOrigin = true;
                            }
                        }
                    }
                    else
                    {
                        // Bouncing back to origin
                        float btDx = attackOrigin.X - Position.X;
                        float btDy = attackOrigin.Y - Position.Y;
                        float btLen = (float)Math.Sqrt(btDx * btDx + btDy * btDy);

                        if (btLen > AttackJumpSpeed)
                        {
                            Position = new PointF(
                                Position.X + (btDx / btLen) * AttackJumpSpeed,
                                Position.Y + (btDy / btLen) * AttackJumpSpeed
                            );
                        }
                        else
                        {
                            Position = attackOrigin;
                            State = EnemyState.Walking;
                        }
                    }
                    break;

                case EnemyState.Knockback:
                    UpdateAnimation(idleFrameCount);

                    if (knockbackRemaining > 0)
                    {
                        float step = Math.Min(EnemyKnockbackSpeed, knockbackRemaining);
                        Position = new PointF(
                            Position.X + knockbackDirection.X * step,
                            Position.Y + knockbackDirection.Y * step
                        );
                        knockbackRemaining -= step;
                    }
                    else
                    {
                        State = EnemyState.Walking;
                    }
                    break;

                case EnemyState.Stunned:
                    UpdateAnimation(idleFrameCount);
                    if (stunRemaining <= 0)
                    {
                        State = EnemyState.Walking;
                    }
                    break;
            }

            // Keep enemies out of the player's hurtbox except during an attack leap
            if (State != EnemyState.Attacking)
                ResolvePlayerOverlap(player);
        }

        private void UpdateAnimation(int frameCount)
        {
            animTimer++;
            if (animTimer >= AnimFrameDuration)
            {
                animTimer = 0;
                animFrame = (animFrame + 1) % frameCount;
            }
        }

        private void UpdateHop()
        {
            hopTimer += HopSpeed;
            hopOffset = (float)Math.Abs(Math.Sin(hopTimer)) * -HopHeight;
        }

        private bool WouldOverlapPlayerHitbox(PointF candidatePosition, Player player)
        {
            RectangleF candidateBounds = new RectangleF(candidatePosition, ObjSize);
            RectangleF paddedPlayerHitbox = RectangleF.Inflate(player.Hitbox, PlayerSeparationBuffer, PlayerSeparationBuffer);
            return candidateBounds.IntersectsWith(paddedPlayerHitbox);
        }

        private void ResolvePlayerOverlap(Player player)
        {
            RectangleF playerBox = RectangleF.Inflate(player.Hitbox, PlayerSeparationBuffer, PlayerSeparationBuffer);
            RectangleF enemyBox = Bounds;

            if (!enemyBox.IntersectsWith(playerBox))
                return;

            float overlapLeft = enemyBox.Right - playerBox.Left;
            float overlapRight = playerBox.Right - enemyBox.Left;
            float overlapTop = enemyBox.Bottom - playerBox.Top;
            float overlapBottom = playerBox.Bottom - enemyBox.Top;

            float pushX = 0f;
            float pushY = 0f;

            if (overlapLeft < overlapRight)
                pushX = -overlapLeft;
            else
                pushX = overlapRight;

            if (overlapTop < overlapBottom)
                pushY = -overlapTop;
            else
                pushY = overlapBottom;

            if (Math.Abs(pushX) < Math.Abs(pushY))
                Position = new PointF(Position.X + pushX, Position.Y);
            else
                Position = new PointF(Position.X, Position.Y + pushY);
        }

        private static PointF GetHorizontalDirection(float deltaX)
        {
            if (Math.Abs(deltaX) < 0.01f)
                return new PointF(1f, 0f);

            return new PointF(Math.Sign(deltaX), 0f);
        }

        // =====================================================================
        // DAMAGE & KNOCKBACK
        // =====================================================================
        public virtual void TakeDamage(int damage, PointF sourcePosition)
        {
            TakeDamage(damage, sourcePosition, EnemyKnockbackDistance);
        }

        public virtual void TakeDamage(int damage, PointF sourcePosition, float knockbackDistance)
        {
            base.TakeDamage(damage);

            if (Health > 0)
            {
                knockbackDirection = GetHorizontalDirection(Position.X - sourcePosition.X);
                knockbackRemaining = knockbackDistance;
                animFrame = 0;
                animTimer = 0;
                State = EnemyState.Knockback;
            }
        }

        public override void OnDeath()
        {
            GameManager.CurPlayer.AddCurrency(1);
            GameManager.OnEnemyDefeated();
            GameManager.DespawnObject(this);
        }

        public virtual void ApplyStun(int durationMs)
        {
            stunRemaining = durationMs;
            State = EnemyState.Stunned;
            animFrame = 0;
            animTimer = 0;
        }

        public void SetMaxHealth(int newMaxHealth)
        {
            base.MaxHealth = newMaxHealth;
            base.Health = newMaxHealth;
        }

        // =====================================================================
        // DRAW
        // =====================================================================
        public override void Draw(Graphics g)
        {
            Bitmap sheet = (State == EnemyState.Idle || State == EnemyState.Knockback || State == EnemyState.Stunned) ? idleSheet : walkSheet;
            int frameCount = (State == EnemyState.Idle || State == EnemyState.Knockback || State == EnemyState.Stunned) ? idleFrameCount : walkFrameCount;

            if (sheet == null) return;

            int safeFrame = Math.Min(animFrame, frameCount - 1);
            float drawX = Position.X;
            float drawY = Position.Y + (State == EnemyState.Walking ? hopOffset : 0f);

            Rectangle srcRect = new Rectangle(safeFrame * 32, 0, 32, 32);
            RectangleF destRect = new RectangleF(drawX, drawY, DrawSize, DrawSize);

            if (facingLeft)
            {
                GraphicsState savedState = g.Save();

                g.TranslateTransform(drawX + DrawSize / 2f, drawY + DrawSize / 2f);
                g.ScaleTransform(-1, 1);
                g.TranslateTransform(-(drawX + DrawSize / 2f), -(drawY + DrawSize / 2f));
                g.DrawImage(sheet, destRect, srcRect, GraphicsUnit.Pixel);

                g.Restore(savedState);
            }
            else
            {
                g.DrawImage(sheet, destRect, srcRect, GraphicsUnit.Pixel);
            }

            DrawHealthBar(g);
        }

        private void DrawHealthBar(Graphics g)
        {
            float barX = Position.X + (DrawSize / 2f) - (HealthBarWidth / 2f);
            float barY = Position.Y - HealthBarOffsetY - HealthBarHeight;

            float healthPercent = (float)Health / MaxHealth;
            float fillWidth = HealthBarWidth * healthPercent;

            using (SolidBrush bgBrush = new SolidBrush(Color.Black))
                g.FillRectangle(bgBrush, barX, barY, HealthBarWidth, HealthBarHeight);

            if (fillWidth > 0)
            {
                using (SolidBrush fillBrush = new SolidBrush(Color.Red))
                    g.FillRectangle(fillBrush, barX, barY, fillWidth, HealthBarHeight);
            }

            using (Pen outlinePen = new Pen(Color.Black, 1f))
                g.DrawRectangle(outlinePen, barX, barY, HealthBarWidth, HealthBarHeight);
        }
    }
}