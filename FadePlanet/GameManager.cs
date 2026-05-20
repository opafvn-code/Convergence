using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FadePlanet
{
    

    public enum RealmType { Starting, Air, Earth, Water, Fire, Boss, Victory }

    public static class GameManager
    {
        public static Player CurPlayer { get; private set; }
        public static readonly Size WindowSize = new Size(1280, 720);
        public static void SetPlayer(Player plr) { CurPlayer = plr; }
        public static bool GameWon { get; private set; } = false;
        public static float VictoryFadeAlpha { get; private set; } = 0f;

        #region Realm Progression
        public static RealmType CurrentRealm { get; private set; } = RealmType.Starting;
        public static int EnemiesDefeatedInRealm { get; private set; } = 0;
        public static int TotalEnemiesToSpawn { get; private set; } = 10;
        public static int EnemiesSpawnedInRealm { get; private set; } = 0;
        public static bool TokenSpawned { get; private set; } = false;
        public static bool BossDefeated { get; private set; } = false;

        public static void ResetRealmState()
        {
            EnemiesDefeatedInRealm = 0;
            EnemiesSpawnedInRealm = 0;
            TokenSpawned = false;
        }

        public static void AdvanceToNextRealm()
        {
            switch (CurrentRealm)
            {
                case RealmType.Starting:
                    CurrentRealm = RealmType.Air;
                    LoadRoom_Two();
                    break;
                case RealmType.Air:
                    CurrentRealm = RealmType.Earth;
                    LoadRoom_Three();
                    break;
                case RealmType.Earth:
                    CurrentRealm = RealmType.Water;
                    LoadRoom_Four();
                    break;
                case RealmType.Water:
                    CurrentRealm = RealmType.Fire;
                    LoadRoom_Five();
                    break;
                case RealmType.Fire:
                    CurrentRealm = RealmType.Boss;
                    LoadRoom_Six();
                    break;
            }
            ResetRealmState();
        }

        public static void OnEnemyDefeated()
        {
            EnemiesDefeatedInRealm++;

            // Notify boss if in boss realm
            if (CurrentRealm == RealmType.Boss)
            {
                Boss boss = GetObjectsByType(ObjectType.Enemy).FirstOrDefault(obj => obj is Boss) as Boss;
                boss?.OnEnemyDefeated();
            }

            // Check if all enemies defeated and token not yet spawned
            if (EnemiesDefeatedInRealm >= TotalEnemiesToSpawn && !TokenSpawned && CurrentRealm != RealmType.Boss)
            {
                SpawnRealmToken();
            }
        }

        public static void SpawnRealmToken()
        {
            TokenSpawned = true;
            ElementType tokenType = ElementType.None;

            switch (CurrentRealm)
            {
                case RealmType.Air:
                    tokenType = ElementType.Air;
                    break;
                case RealmType.Earth:
                    tokenType = ElementType.Earth;
                    break;
                case RealmType.Water:
                    tokenType = ElementType.Water;
                    break;
                case RealmType.Fire:
                    tokenType = ElementType.Fire;
                    break;
            }

            if (tokenType != ElementType.None)
            {
                // Spawn token at center of screen
                Point tokenPos = new Point(640 - 33, 360 - 33);
                new Item(tokenPos, new Size(67, 67), ItemType.Token, tokenType);
            }
        }

        public static void OnTokenCollected()
        {
            AdvanceToNextRealm();
        }

        public static void TriggerVictory()
        {
            if (GameWon) return;
            GameWon = true;
            VictoryFadeAlpha = 0f;
            CurrentRealm = RealmType.Victory;
        }

        public static void UpdateVictoryFade()
        {
            if (GameWon && VictoryFadeAlpha < 1f)
            {
                VictoryFadeAlpha += 0.01f;
                if (VictoryFadeAlpha > 1f)
                    VictoryFadeAlpha = 1f;
            }
        }

        public static void TrySpawnEnemies()
        {
            if (CurrentRealm == RealmType.Starting || CurrentRealm == RealmType.Boss || CurrentRealm == RealmType.Victory)
                return;

            // Spawn enemies if we haven't reached the total yet
            int enemiesAlive = GetObjectsByType(ObjectType.Enemy).Count;
            int enemiesToSpawn = Math.Min(2, TotalEnemiesToSpawn - EnemiesSpawnedInRealm);

            if (enemiesToSpawn > 0 && enemiesAlive < 3)
            {
                for (int i = 0; i < enemiesToSpawn; i++)
                {
                    SpawnEnemyForCurrentRealm();
                    EnemiesSpawnedInRealm++;
                }
            }
        }

        private static void SpawnEnemyForCurrentRealm()
        {
            EnemyType enemyType = EnemyType.Air;

            switch (CurrentRealm)
            {
                case RealmType.Air:
                    enemyType = EnemyType.Air;
                    break;
                case RealmType.Earth:
                    enemyType = EnemyType.Earth;
                    break;
                case RealmType.Water:
                    enemyType = EnemyType.Water;
                    break;
                case RealmType.Fire:
                    enemyType = EnemyType.Fire;
                    break;
            }

            // Random spawn position around the screen (not just right side)
            Random rand = new Random();
            int x = rand.Next(100, 1100);
            int y = rand.Next(150, 550);
            Point spawnPos = new Point(x, y);

            Enemy enemy = new Enemy(spawnPos, new Size((int)(32 * 3.0f), (int)(32 * 3.0f)), enemyType);
        }

        private static void SpawnBoss()
        {
            // Position boss: center Y (360 - 245 = 115), right X but not cutoff (1280 - 490 = 790)
            Point bossPos = new Point(790, 115);
            Boss boss = new Boss(bossPos, new Size(490, 490));
        }
        #endregion

        #region Object Management

        private static readonly Dictionary<ObjectType, Dictionary<int, WorldObject>> RoomObjects = new Dictionary<ObjectType, Dictionary<int, WorldObject>>();
        public static IReadOnlyDictionary<ObjectType, Dictionary<int, WorldObject>> AllObjects => RoomObjects;
        public static int _idCounter = 0;

        public static void SpawnObject(WorldObject obj)
        {
            obj.Id = _idCounter++;

            if (!RoomObjects.ContainsKey(obj.Type))
            {
                RoomObjects[obj.Type] = new Dictionary<int, WorldObject>();
            }

            RoomObjects[obj.Type].Add(obj.Id, obj);
        }

        public static void DespawnObject(WorldObject obj)
        {
            if (RoomObjects.TryGetValue(obj.Type, out var categoryDict))
            {
                categoryDict.Remove(obj.Id);
            }
        }

        //Helper for spawning enemies into rooms
        private static Enemy SpawnEnemy(EnemyType type, Point pos)
        {
            return new Enemy(pos, new Size((int)(32 * 3.0f), (int)(32 * 3.0f)), type);
        }
        #endregion

        #region Object Updates & Rendering


        // Retrieves all objects of a specific type from GameManager.
        // Returns a list of objects for direct modification or querying.
        public static List<WorldObject> GetObjectsByType(ObjectType type)
        {
            if (RoomObjects.TryGetValue(type, out var objectDict))
            {
                return objectDict.Values.ToList();
            }
            return new List<WorldObject>();
        }

        //Updates all objects of a specific type from GameManager with the provided action.
        public static void UpdateObjectType(ObjectType type, Action<WorldObject> updateAction)
        {
            if (RoomObjects.TryGetValue(type, out var objectDict))
            {
                foreach (WorldObject obj in objectDict.Values.ToList())
                {
                    updateAction(obj);
                }
            }
        }


        // Draws all objects of a specific type from GameManager.
        // Optionally filters objects using a predicate.
        public static void DrawObjectType(Graphics g, ObjectType type, Func<WorldObject, bool> filter = null)
        {
            if (RoomObjects.TryGetValue(type, out var objectDict))
            {
                foreach (WorldObject obj in objectDict.Values)
                {
                    if (filter == null || filter(obj))
                    {
                        obj.Draw(g);
                    }
                }
            }
        }

        #endregion

        #region Rooms
        public static void LoadRoom_One()
        {
            //Load background, sound, etc.

            //Objects
            SetPlayer( new Player(new Point(528, 250), new Size(224, 224)) );

            new OldMan(new Point(800, 250), new Size(250, 250));

            ResetRealmState();
        }
        public static void LoadRoom_Two()
        {
            //Air Realm
            //Clear all existing objects except player
            ClearAllObjectsExceptPlayer();

            //Reset player position to left side
            CurPlayer.Position = new PointF(100, 250);

            ResetRealmState();
        }
        public static void LoadRoom_Three()
        {
            //Earth Realm
            ClearAllObjectsExceptPlayer();
            CurPlayer.Position = new PointF(100, 250);
            ResetRealmState();
        }
        public static void LoadRoom_Four()
        {
            //Water Realm
            ClearAllObjectsExceptPlayer();
            CurPlayer.Position = new PointF(100, 250);
            ResetRealmState();
        }
        public static void LoadRoom_Five()
        {
            //Fire Realm
            ClearAllObjectsExceptPlayer();
            CurPlayer.Position = new PointF(100, 250);
            ResetRealmState();
        }
        public static void LoadRoom_Six()
        {
            //Boss Realm
            ClearAllObjectsExceptPlayer();
            CurPlayer.Position = new PointF(100, 250);
            ResetRealmState();

            //Spawn the boss
            SpawnBoss();
        }

        private static void ClearAllObjectsExceptPlayer()
        {
            //Clear all enemies
            var enemies = GetObjectsByType(ObjectType.Enemy).ToList();
            foreach (var enemy in enemies)
            {
                DespawnObject(enemy);
            }

            //Clear all items
            var items = GetObjectsByType(ObjectType.Item).ToList();
            foreach (var item in items)
            {
                DespawnObject(item);
            }

            //Clear all projectiles
            var projectiles = GetObjectsByType(ObjectType.Projectile).ToList();
            foreach (var proj in projectiles)
            {
                DespawnObject(proj);
            }

            //Clear all friendlies (old man)
            var friendlies = GetObjectsByType(ObjectType.Friendly).ToList();
            foreach (var friendly in friendlies)
            {
                DespawnObject(friendly);
            }
        }
        #endregion
    }
}