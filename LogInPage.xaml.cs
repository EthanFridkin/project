using Battleship.Core.Models;

namespace Project
{
    public partial class LoginPage : ContentPage
    {
        private readonly FirebaseService _firebase;

        public LoginPage(FirebaseService firebase)
        {
            InitializeComponent();
            _firebase = firebase;
        }

        private async void Login_Click(object sender, EventArgs e)
        {
            if (!ValidateInput()) return;
            SetLoading(true);

            var (success, error) = await _firebase.LoginAsync(
                UsernameEntry.Text.Trim(),
                PasswordEntry.Text);

            SetLoading(false);

            if (success)
            {
                await DisplayAlert("ברוך שובך", $"ברוך שובך {_firebase.Username}", "המשך");
                var player = new Player(1, _firebase.Username!);
                await Navigation.PushAsync(new GameModePage(player, _firebase));
            }
            else { ShowError(error); }
        }

        private async void Register_Click(object sender, EventArgs e)
        {
            if (!ValidateInput()) return;
            SetLoading(true);

            var (success, error) = await _firebase.RegisterAsync(
                UsernameEntry.Text.Trim(),
                PasswordEntry.Text);

            SetLoading(false);

            if (success)
            {
                await DisplayAlert("ברוך הבא", $"ברוך הבא {_firebase.Username}", "המשך");
                var player = new Player(1, _firebase.Username!);
                await Navigation.PushAsync(new GameModePage(player, _firebase));
            }
            else { ShowError(error); }
        }

        private bool ValidateInput()
        {
            ErrorLabel.IsVisible = false;

            if (string.IsNullOrWhiteSpace(UsernameEntry.Text))
            {
                ShowError("נא להזין שם משתמש");
                return false;
            }

            if (string.IsNullOrWhiteSpace(PasswordEntry.Text) || PasswordEntry.Text.Length < 6)
            {
                ShowError("סיסמה חייבת להכיל לפחות 6 תווים");
                return false;
            }

            return true;
        }

        private void ShowError(string msg)
        {
            ErrorLabel.Text = msg;
            ErrorLabel.IsVisible = true;
        }

        private void SetLoading(bool loading)
        {
            LoadingIndicator.IsRunning = loading;
            LoadingIndicator.IsVisible = loading;
            LoginBtn.IsEnabled = !loading;
            RegisterBtn.IsEnabled = !loading;
        }
    }
}
