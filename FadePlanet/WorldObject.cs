using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FadePlanet
{
    public enum ObjectType
    {
        None,
        Wall,
        Enemy,
        Boss,
        OldMan,
        Player,
        Item,
        Projectile,
        Friendly
    }

    public enum ObjectState
    {
        None,
        Idle,
        Move,
        Attack,
        Death
    }

    public enum ElementType
    {
        None,
        Water,
        Fire,
        Earth,
        Air
    }

    public class WorldObject
    {
        public int Id { get; set; }

        public PointF Position { get; set; }
        public SizeF ObjSize { get; set; }
        public ObjectType Type { get; set; }

        // Unified Health System
        public virtual int Health { get; protected set; }
        public virtual int MaxHealth { get; protected set; }

        public RectangleF Bounds => new RectangleF(Position, ObjSize);

        // Hitbox — overridden in Player with more precise dimensions
        public virtual RectangleF Hitbox => Bounds;

        // Hitbox visibility toggle
        public virtual bool ShowHitbox { get; set; } = false;

        #region Animations
        public Dictionary<ObjectState, Bitmap> Animations = new Dictionary<ObjectState, Bitmap>();
        public ObjectState CurrentState { get; set; }
        public int CurrentFrame { get; set; } = 0;

        public Bitmap GetCurrentSheet() => Animations[CurrentState];
        #endregion

        public WorldObject(PointF pos, SizeF size, ObjectType type = ObjectType.None)
        {
            Position = pos;
            ObjSize = size;
            Type = type;
            GameManager.SpawnObject(this);
        }

        public virtual void Draw(Graphics g) { }
        public virtual void OnInteract(Player player) { }
        public virtual void TakeDamage(int damage)
        {
            Health -= damage;
            if (Health <= 0)
            {
                Health = 0;
                OnDeath();
            }
        }
        public virtual void OnDeath() { }
    }
}