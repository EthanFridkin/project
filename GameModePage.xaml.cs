using Battleship.Core.Models;

namespace Project
{
    public partial class GameModePage : ContentPage
    {
        private readonly Player _player;
        private readonly FirebaseService _firebase;
        private CancellationTokenSource _cts = new();
        private bool _searching = false;

        public GameModePage(Player player, FirebaseService firebase)
        {
            InitializeComponent();
            _player = player;
            _firebase = firebase;
        }

        private async void PlayVsAI_Click(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new GamePage(
                player: _player,
                firebase: _firebase,
                isGuest: false,
                isMulti: false,
                isGemini: false));
        }

        private async void PlayVsGemini_Click(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new GamePage(
                player: _player,
                firebase: _firebase,
                isGuest: false,
                isMulti: false,
                isGemini: true));
        }

        private async void PlayVsPlayer_Click(object sender, EventArgs e)
        {
            if (_searching) return;
            _searching = true;
            SetSearching(true);

            try
            {
                await _firebase.CleanOldRoomsAsync();
                WaitingLabel.Text = "מחפש יריב...";

                var (roomId, isPlayer1, opponentName) =
                    await _firebase.FindOrCreateRoomAsync();

                if (!isPlayer1)
                {
                    await NavigateToGame(opponentName);
                    return;
                }

                WaitingLabel.Text = "ממתין לשחקן שני...";
                var token = _cts.Token;

                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token);
                    var (joined, name) = await _firebase.CheckRoomStatusAsync();
                    if (joined)
                    {
                        await NavigateToGame(name);
                        return;
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                await DisplayAlert("שגיאה", ex.Message, "אישור");
            }
            finally
            {
                _searching = false;
                SetSearching(false);
            }
        }

        private async Task NavigateToGame(string opponentName)
        {
            _cts.Cancel();
            WaitingLabel.Text = $"נמצא יריב - {opponentName}";
            await Task.Delay(1000);

            await Navigation.PushAsync(new GamePage(
                player: _player,
                firebase: _firebase,
                isGuest: false,
                isMulti: true,
                isGemini: false,
                opponentName: opponentName));

            SetSearching(false);
            _searching = false;
            _cts = new CancellationTokenSource();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            _cts.Cancel();
            _searching = false;
            SetSearching(false);
            _cts = new CancellationTokenSource();
        }

        private void SetSearching(bool searching)
        {
            WaitingSection.IsVisible = searching;
            WaitingSpinner.IsRunning = searching;
            AIBtn.IsEnabled = !searching;
            GeminiBtn.IsEnabled = !searching;
            MultiBtn.IsEnabled = !searching;
        }
    }
}