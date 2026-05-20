using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FadePlanet
{
    internal class Ripple : WorldObject
    {
        private float CurrentRadius { get; set; } = 0f;
        private const float MaxRadius = 250f;
        private const float ExpandSpeed = 4.0f;
        private const float KnockbackDistance = 40f;
        private const float KnockbackSpeed = 3f;
        private const int StunDurationMs = 3000; // 3 seconds
        private Color RippleColor = Color.FromArgb(100, 100, 150, 255); // Semi-transparent blue

        private bool hasHit = false;

        public Ripple(PointF playerPos) : base(playerPos, new SizeF(0, 0), ObjectType.None)
        {
            // Center the ripple on the player
            Position = new PointF(playerPos.X + 112f, playerPos.Y + 112f);
        }

        public override void Draw(Graphics g)
        {
            if (CurrentRadius > 0)
            {
                using (Pen pen = new Pen(Color.FromArgb(150, 100, 150, 255), 2f))
                {
                    float x = Position.X - CurrentRadius;
                    float y = Position.Y - CurrentRadius;
                    float diameter = CurrentRadius * 2;
                    g.DrawEllipse(pen, x, y, diameter, diameter);
                }

                using (Brush brush = new SolidBrush(RippleColor))
                {
                    float x = Position.X - CurrentRadius;
                    float y = Position.Y - CurrentRadius;
                    float diameter = CurrentRadius * 2;
                    g.FillEllipse(brush, x, y, diameter, diameter);
                }
            }
        }

        public void Update()
        {
            // Expand the ripple
            CurrentRadius += ExpandSpeed;

            // Check for collisions with enemies
            if (!hasHit && CurrentRadius >= MaxRadius * 0.5f) // Start checking halfway through expansion
            {
                CheckEnemyCollisions();
            }

            // Despawn when fully expanded
            if (CurrentRadius >= MaxRadius)
            {
                GameManager.DespawnObject(this);
                GameManager.CurPlayer.SetMovementState(true); // Reenable player movement
            }
        }

        private void CheckEnemyCollisions()
        {
            if (GameManager.AllObjects.TryGetValue(ObjectType.Enemy, out var enemyDict))
            {
                foreach (WorldObject obj in enemyDict.Values.ToList())
                {
                    if (obj is Boss) continue;
                    if (obj is Enemy enemy)
                    {
                        float dx = enemy.Position.X + 48f - Position.X;
                        float dy = enemy.Position.Y + 48f - Position.Y;
                        float distanceToEnemy = (float)Math.Sqrt(dx * dx + dy * dy);

                        // Check if enemy is within the current ripple radius
                        if (distanceToEnemy <= CurrentRadius && distanceToEnemy > (CurrentRadius - ExpandSpeed * 2))
                        {
                            // Apply horizontal knockback away from ripple center
                            float horizontalDir = Math.Abs(dx) < 0.01f ? 1f : Math.Sign(dx);

                            enemy.Position = new PointF(
                                enemy.Position.X + horizontalDir * KnockbackDistance,
                                enemy.Position.Y
                            );

                            // Apply stun
                            enemy.ApplyStun(StunDurationMs);
                        }
                    }
                }
            }
        }
    }
}
