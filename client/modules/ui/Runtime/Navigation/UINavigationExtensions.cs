using UnityEngine.UI;

namespace Global.UI
{
    public static class UINavigationExtensions
    {
        // The selectable can be selected by the gamepad (a candidate of the window it is in). The menu kit and its
        // prefabs have Navigation.None everywhere, which keeps a selectable out (the arrows of a slider, the field
        // around the character cards that closes the panel).
        public static void EnableNavigation(this Selectable selectable)
        {
            selectable.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        }
    }
}
