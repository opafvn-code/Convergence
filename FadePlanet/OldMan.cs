using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace FadePlanet
{
    internal class OldMan : WorldObject
    {
        public const float InteractDistance = 200f;
        private bool firstInteraction = true;
        private Image ManImage;
        public bool IsInteracting { get; private set; } = false; //Prevent multiple windows popping up
        public bool HasInteracted { get; set; } = false; //prevent window from reopening after player exits

        
        private DialogForm DialogWindow;

        #region Hitbox
        private const float HitboxWidth = 200f;
        private const float HitboxHeight = 200f;
        private const float HitboxOffsetX = 25f;
        private const float HitboxOffsetY = 50f;

        // Override the base WorldObject Hitbox with a more precise player hitbox
        public override RectangleF Hitbox => new RectangleF(
            Position.X + HitboxOffsetX,
            Position.Y + HitboxOffsetY,
            HitboxWidth,
            HitboxHeight
        );
        #endregion

        public OldMan(PointF pos, SizeF size, ObjectType type = ObjectType.Friendly) : base(pos, size, type)
        {
            LoadImage();
        }

        private void LoadImage()
        {
            try
            {
                string imagePath = Path.Combine(Application.StartupPath, @"..\..\Graphics\Other Chars\WiseOldman.png");
                ManImage = Image.FromFile(imagePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error laoding old man: " + ex.Message);
            }
        }
        public override void OnInteract(Player player)
        {
            IsInteracting = true;
            GameManager.CurPlayer.SetMovementState(false);

            if (firstInteraction)
            {
                ShowTutorial();
                firstInteraction = false;
            }
            else
            {
                ShowMenu();
            }

            IsInteracting = false;
            GameManager.CurPlayer.SetMovementState(true);
        }
        
        public override void Draw(Graphics g)
        {
            if (ManImage == null) return;

            g.DrawImage(
                ManImage,
                new RectangleF(
                    Position.X,
                    Position.Y,
                    ObjSize.Width,
                    ObjSize.Height
                )
            );
        }
        private void ShowTutorial()
        {
            string tutorialText = "Welcome, young one!\n\n" +
                "The world's life force has been drained, and you must restore it!\n\n" +
                "YOUR QUEST:\n" +
                "You must travel through the elemental realms in order:\n\n" +
                "1. AIR REALM - Defeat the air enemies\n" +
                "2. EARTH REALM - Defeat the earth enemies\n" +
                "3. WATER REALM - Defeat the water enemies\n" +
                "4. FIRE REALM - Defeat the fire enemies\n" +
                "5. BOSS REALM - Defeat the final boss\n\n" +
                "Each realm has waves of enemies. Defeat them all to spawn a token.\n" +
                "Collect the token to teleport to the next realm.\n\n" +
                "CONTROLS:\n" +
                "• RIGHT-CLICK: Basic sword attack\n" +
                "• LEFT-CLICK: Attack with your current ability\n" +
                "• 1-4 KEYS: Switch between elemental scrolls\n" +
                "• 5 KEY: Select healing potion\n\n" +
                "ABILITIES:\n" +
                "   • Fire: Shoot fireballs at enemies\n" +
                "   • Water: Create protective ripples\n" +
                "   • Earth: Raise rock barriers for defense\n" +
                "   • Air: Swift aerial attacks\n\n" +
                "Defeat enemies to earn currency. Use it at my shop to buy potions.";

            DialogResult result = MessageBox.Show(tutorialText, "Your Quest", MessageBoxButtons.OK);
            if (result == DialogResult.OK)
            {
                ShowMenu();
            }
        }

        private void ShowMenu()
        {

            string menuText = "Greetings again, traveler!\n\nWhat can I help you with?";

            using (DialogWindow = new DialogForm(menuText,"Old Man", "Begin Quest", "Tutorial", "Shop", "Nevermind"))
            {
                DialogWindow.ShowDialog();

                string selectedButton = (string)DialogWindow.Tag;

                switch (selectedButton)
                {
                    case "Begin Quest":
                        BeginQuest();
                        break;
                    case "Tutorial":
                        ShowTutorialAgain();
                        break;
                    case "Shop":
                        ShowShop();
                        break;
                    case "Nevermind":
                        //Close form
                        IsInteracting = false;
                        GameManager.CurPlayer.SetMovementState(true);
                        DialogWindow.Close();
                        break;
                }
            }
        }

        private void BeginQuest()
        {
            string questText = "Good luck, brave warrior!\n\n" +
                "Head to the right to begin your journey through the Air Realm.\n" +
                "Defeat all enemies to claim the Air Token and proceed to the next realm.";

            MessageBox.Show(questText, "Begin Quest", MessageBoxButtons.OK);
            GameManager.AdvanceToNextRealm();
        }

        private void ShowTutorialAgain()
        {
            string tutorialText = "Here's a reminder of the basics:\n\n" +
                "• LEFT-CLICK: Attack with your current ability\n" +
                "• RIGHT-CLICK: Basic sword attack\n" +
                "• 1-4 KEYS: Switch between elemental scrolls\n" +
                "• LEFT-CLICK: With the potion icon selected in the hotbar, consume a healing potion\n" +
                "• Collect token from realm after having defeated all enemies\n" +
                "• Use your currency to buy potions from my shop\n\n" +
                "Good luck out there!";

            DialogResult result = MessageBox.Show(tutorialText, "Tutorial Reminder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (result == DialogResult.OK)
            {
                ShowMenu();
            }
        }

        private void ShowShop()
        {
            string shopText = "Welcome to my shop!\n\n" +
                "I sell healing potions for 10 crystals each.\n" +
                "(Shop functionality coming soon...)";

            using (DialogWindow = new DialogForm("Old Man's Shop", shopText, "Potion - $10", "Nevermind"))
            {
                DialogWindow.ShowDialog();

                string selectedButton = (string)DialogWindow.Tag;

                switch(selectedButton)
                {
                    case "Potion - $10":
                        if (GameManager.CurPlayer.Currency >= 10)
                        {
                            // Subtract 10 currency and add 1 potion
                            GameManager.CurPlayer.AddCurrency(-10);
                            GameManager.CurPlayer.AddPotions(1);
                            MessageBox.Show("Thank you for your purchase!", "Purchase confirmed", MessageBoxButtons.OK);
                        }
                        else
                        {
                            MessageBox.Show("Don't try that again...", "Purchase failed - Your too poor!", MessageBoxButtons.OK);
                        }

                        break;
                    case "Nevermind":
                        // Return to menu
                        ShowMenu();
                        break;
                }

                
            }
        }
    }
}
