using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FadePlanet
{
    public class Abilities
    {
        public interface IElement
        {
            ElementType Type { get; }
            float StaminaCost { get; }
            float CooldownMs { get; }
            float CooldownRemaining { get; set; }
            bool PrimaryAttack(Player player);
            void UpdateCooldown(float deltaTimeMs);

        }

        public abstract class ScrollBase : IElement
        {
            public abstract ElementType Type { get; }
            public abstract float StaminaCost { get; }
            public abstract float CooldownMs { get; }
            public float CooldownRemaining { get; set; } = 0f;

            public abstract bool PrimaryAttack(Player player);

            public virtual void UpdateCooldown(float deltaTimeMs)
            {
                if (CooldownRemaining > 0)
                {
                    CooldownRemaining = Math.Max(0, CooldownRemaining - deltaTimeMs);
                }
            }
        }

        public class FireScroll : ScrollBase
        {
            public override ElementType Type => ElementType.Fire;
            public override float StaminaCost => 5f;
            public override float CooldownMs => 500f; // 0.5 second cooldown

            public override bool PrimaryAttack(Player player)
            {
                if (!player.CanUseAbility(StaminaCost)) return false;
                player.UseStamina(StaminaCost);
                ShootFireball(player);
               
                return true;
            }
            
            public void ShootFireball(Player player)
            {
                PointF dir = player.GetAttackDirection();

                // Calculate spawn position based on direction
                float posX = player.Position.X + player.ObjSize.Width / 2;
                float posY = player.Position.Y + player.ObjSize.Height / 2;

                // Offset spawn position in the direction being fired
                if (dir.X < 0) posX = player.Position.X;
                if (dir.X > 0) posX = player.Position.X + player.ObjSize.Width;
                if (dir.Y < 0) posY = player.Position.Y;
                if (dir.Y > 0) posY = player.Position.Y + player.ObjSize.Height;

                new Projectile(new PointF(posX, posY), new SizeF(64, 64), ElementType.Fire, dir);
            }
        }
        public class WaterScroll : ScrollBase
        {
            public override ElementType Type => ElementType.Water;
            public override float StaminaCost => 20f;
            public override float CooldownMs => 1500f; // 1.5 second cooldown

            public override bool PrimaryAttack(Player player)
            {
                if (!player.CanUseAbility(StaminaCost)) return false;
                player.UseStamina(StaminaCost);
                SpawnRipple(player);
                
                return true;
            }
            public void SpawnRipple(Player player)
            {
                new Ripple(player.Position);
            }

        }
        public class EarthScroll : ScrollBase
        {
            public override ElementType Type => ElementType.Earth;
            public override float StaminaCost => 25f;
            public override float CooldownMs => 2000f; // 2 second cooldown

            public override bool PrimaryAttack(Player player)
            {
                if (!player.CanUseAbility(StaminaCost)) return false;
                player.UseStamina(StaminaCost);
                
                return true;
            }

        }
        public class AirScroll : ScrollBase
        {
            public override ElementType Type => ElementType.Air;
            public override float StaminaCost => 5f;
            public override float CooldownMs => 800f; // 0.8 second cooldown

            public override bool PrimaryAttack(Player player)
            {
                if (!player.CanUseAbility(StaminaCost)) return false;
                player.UseStamina(StaminaCost);
               
                return true;
            }

        }
    }
}