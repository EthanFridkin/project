using Battleship.Core.Models;

namespace Project
{
    public partial class MainPage : ContentPage
    {
        private readonly FirebaseService _firebase = new FirebaseService();

        public MainPage()
        {
            InitializeComponent();
        }

        private async void UserLogin_Click(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LoginPage(_firebase));
        }

        private async void GuestLogin_Click(object sender, EventArgs e)
        {
            var player = new Player(1, "אורח");
            await Navigation.PushAsync(new GameModePage(player, _firebase));
        }

        private async void Instructions_Click(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new InstructionsPage());
        }
    }
}