using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FadePlanet
{
    internal class Projectile : WorldObject
    {
        ElementType ProjType { get; set; }
        private Color Col;
        private float Speed = 10.0f;
        private PointF Direction;
        private int ProjectileDamage = 15;

        // Animation for fireball spritesheet
        private Bitmap fireballSheet;
        private int animFrame = 0;
        private int animTimer = 0;
        private const int AnimFrameDuration = 4;
        private const int FireballFrameWidth = 32;
        private const int FireballFrameHeight = 32;
        private const int FireballTotalFrames = 3;

        public Projectile(PointF pos, SizeF size, ElementType element, PointF dir, ObjectType type = ObjectType.Projectile) : base(pos, size, type)
        {
            ProjType = element;
            Direction = dir;
            SetType();
            LoadImages();
        }

        public Projectile(PointF pos, SizeF size, PointF dir, float speed, ElementType element, int damage) : base(pos, size, ObjectType.Projectile)
        {
            ProjType = element;
            Direction = dir;
            Speed = speed;
            ProjectileDamage = damage;
            SetType();
            LoadImages();
        }

        private void SetType()
        {
            switch (ProjType)
            {
                case ElementType.Air:
                    Col = Color.White;
                    ProjectileDamage = 10;
                    break;
                case ElementType.Water:
                    Col = Color.Blue;
                    ProjectileDamage = 20;
                    break;
                case ElementType.Fire:
                    Col = Color.Red;
                    ProjectileDamage = 25;
                    break;
                case ElementType.Earth:
                    Col = Color.Gray;
                    ProjectileDamage = 50;
                    break;
            }
        }

        private string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\"));
        }

        private void LoadImages()
        {
            if (ProjType == ElementType.Fire)
            {
                try
                {
                    // Load fireball spritesheet from Graphics\Player\Attacks folder (3 frames, 32x32)
                    string basePath = GetProjectRoot();
                    fireballSheet = new Bitmap(Path.Combine(basePath, @"Graphics\Player\Attacks\Fireball.png"));
                }
                catch
                {
                    // Fall back to simple circle if image fails to load
                }
            }
        }

        public override void Draw(Graphics g)
        {
            if (ProjType == ElementType.Fire && fireballSheet != null)
            {
                // Draw animated fireball from spritesheet
                Rectangle srcRect = new Rectangle(animFrame * FireballFrameWidth, 0, FireballFrameWidth, FireballFrameHeight);
                RectangleF destRect = new RectangleF(Position.X, Position.Y, ObjSize.Width, ObjSize.Height);
                g.DrawImage(fireballSheet, destRect, srcRect, GraphicsUnit.Pixel);
            }
            else
            {
                // Draw simple colored circle for other elements
                using (Pen pen = new Pen(Col))
                {
                    RectangleF rect = new RectangleF(Position, ObjSize);
                    g.FillEllipse(new SolidBrush(Col), rect);
                }
            }
        }

        public void Update()
        {
            // Update animation
            if (ProjType == ElementType.Fire && fireballSheet != null)
            {
                animTimer++;
                if (animTimer >= AnimFrameDuration)
                {
                    animTimer = 0;
                    animFrame = (animFrame + 1) % FireballTotalFrames;
                }
            }

            float x = Position.X;
            float y = Position.Y;

            PointF pos = new PointF(x + Direction.X * Speed, y + Direction.Y * Speed);

            Position = pos;

            // Check for collisions with enemies and walls
            CheckImpact();

            // Despawn if off-screen
            if (Position.X < -50 || Position.X > 1350)
            {
                GameManager.DespawnObject(this);
            }
        }

        public void CheckImpact()
        {
            // Check collision with enemies
            if (GameManager.AllObjects.TryGetValue(ObjectType.Enemy, out var enemyDict))
            {
                foreach (WorldObject obj in enemyDict.Values.ToList())
                {
                    if (Bounds.IntersectsWith(obj.Bounds))
                    {
                        // Check if it's a boss - only damage in final phase
                        if (obj is Boss boss)
                        {
                            if (boss.CurrentPhase == BossPhase.Final)
                            {
                                boss.TakeDamage(ProjectileDamage);
                            }
                        }
                        else if (obj is Enemy enemy)
                        {
                            enemy.TakeDamage(ProjectileDamage, Position);
                        }
                        GameManager.DespawnObject(this);
                        return;
                    }
                }
            }

            // Check collision with player (for boss fireballs)
            if (ProjType == ElementType.Fire && GameManager.CurPlayer != null)
            {
                if (Bounds.IntersectsWith(GameManager.CurPlayer.Hitbox))
                {
                    GameManager.CurPlayer.TakeDamage(ProjectileDamage);
                    GameManager.CurPlayer.ApplyKnockback(Position);
                    GameManager.DespawnObject(this);
                    return;
                }
            }

            // Check collision with walls
            if (GameManager.AllObjects.TryGetValue(ObjectType.Wall, out var wallDict))
            {
                foreach (WorldObject wall in wallDict.Values)
                {
                    if (Bounds.IntersectsWith(wall.Hitbox))
                    {
                        GameManager.DespawnObject(this);
                        return;
                    }
                }
            }
        }
    }
}
