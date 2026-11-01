using System;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Menus
{
    /// <summary>
    /// The original's main menu (<c>Gui\MainMenu\MainMenu.cs</c> and the menu methods of <c>InputManager</c>,
    /// RECONSTRUCTION 9.2): menu.json's levels, the selected level and the selected item. The world changes it only
    /// while applying the menu's commands; presentation reads <see cref="Level"/>, <see cref="Item"/>, the title and the
    /// description. The selection survives closing the menu, as the original's did: it is never reset on open or close.
    /// </summary>
    public sealed class MainMenu
    {
        readonly MenuData _data;

        public MainMenu(MenuData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public MenuData Data => _data;

        /// <summary>The level shown (<c>SelectedLevel</c>); the root, 0, at start.</summary>
        public int Level { get; private set; }

        /// <summary>The highlighted item of <see cref="Level"/> (<c>SelectedItem</c>); the first, 0, at start.</summary>
        public int Item { get; private set; }

        public MenuLevel CurrentLevel => _data.Levels[Level];

        public MenuItem CurrentItem => CurrentLevel.Items[Item];

        /// <summary>The level's title (<c>GetSelectedMenuLevelTitle</c>), written at menu.json's <c>titlePosition</c>.</summary>
        public string Title => CurrentLevel.Title;

        /// <summary>The highlighted item's description (<c>GetSelectedMenuItemDescription</c>), empty for most items.</summary>
        public string Description => CurrentItem.Description ?? string.Empty;

        /// <summary>
        /// <c>AtRootMenuLevel</c>: the highlighted item's parent level is -1. Back from here closes the menu instead of
        /// going up a level.
        /// </summary>
        public bool AtRoot => CurrentLevel.Parent < 0;

        /// <summary>Where item <paramref name="index"/> of the shown level is written (the original's <c>(50, 100 + 50 * index)</c>).</summary>
        public PixelVector ItemPosition(int index) => new PixelVector(_data.ItemX, _data.ItemY + _data.ItemStep * index);

        /// <summary>The arrow's top-left (<c>MainMenu.Update</c>: the highlighted item's position, 18 px to its left).</summary>
        public PixelVector ArrowPosition
        {
            get
            {
                var item = ItemPosition(Item);
                return new PixelVector(item.X + _data.ArrowOffset, item.Y);
            }
        }

        /// <summary><c>Action_MenuChoiceUp</c>: the previous item, wrapping from the first to the last.</summary>
        public void Up()
        {
            Item = Item == 0 ? CurrentLevel.Items.Count - 1 : Item - 1;
        }

        /// <summary><c>Action_MenuChoiceDown</c>: the next item, wrapping from the last to the first.</summary>
        public void Down()
        {
            Item = Item == CurrentLevel.Items.Count - 1 ? 0 : Item + 1;
        }

        /// <summary>
        /// <c>Action_GoToParentMenuLevel</c>: up to the parent level with its first item highlighted. Does nothing at the
        /// root (the caller closes the menu there instead, as <c>CheckInput_MainMenu</c> did).
        /// </summary>
        public void Back()
        {
            if (AtRoot)
            {
                return;
            }

            Level = CurrentLevel.Parent;
            Item = 0;
        }

        /// <summary>
        /// <c>Action_SelectChoice</c>: an Open item shows its level; then, whatever the item did, the first item of the
        /// level now shown is highlighted. Returns the item's action for the world to carry out (Quit, ToggleFullscreen).
        /// </summary>
        public MenuAction Select()
        {
            var item = CurrentItem;
            if (item.Action == MenuAction.Open)
            {
                Level = item.Target;
            }

            Item = 0;
            return item.Action;
        }
    }
}
