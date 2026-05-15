using System.Collections.ObjectModel;
using Battleship.Core.Models;

namespace Project
{
    public class GameRecordDisplay
    {
        public string WonIcon = "";
        public string Date = "";
        public string ShotsText = "";
        public string AccuracyText = "";
    }

    public partial class StatsPage : ContentPage
    {
        private readonly FirebaseService _firebase;
        private readonly bool _isGuest;
        private readonly Player _player;

        public StatsPage(
            bool playerWon,
            string playerName,
            int playerShots,
            double playerAccuracy,
            int aiShots,
            double aiAccuracy,
            FirebaseService firebase,
            bool isGuest = false,
            bool newRecord = false,
            Player? player = null)
        {
            InitializeComponent();

            _firebase = firebase;
            _isGuest = isGuest;
            _player = player ?? new Player(1, playerName);

            ResultLabel.Text = playerWon ? "ניצחת" : "יריב ניצח";
            ResultLabel.TextColor = playerWon
                ? Color.FromArgb("#00FF88")
                : Color.FromArgb("#FF4444");

            if (newRecord)
                NewRecordLabel.IsVisible = true;

            PlayerShotsLabel.Text = $"יריות: {playerShots}";
            PlayerAccuracyLabel.Text = $"דיוק: {playerAccuracy:F1}%";
            AIShotsLabel.Text = $"יריות: {aiShots}";
            AIAccuracyLabel.Text = $"דיוק: {aiAccuracy:F1}%";

            if (isGuest || !firebase.IsLoggedIn)
            {
                OverallStatsSection.IsVisible = false;
                HistorySection.IsVisible = false;
            }
            else
            {
                _ = LoadHistoryAsync();
            }
        }

        private async Task LoadHistoryAsync()
        {
            var (profile, games) = await _firebase.GetUserStatsAsync();

            StatsLoader.IsRunning = false;
            StatsLoader.IsVisible = false;

            if (profile != null)
            {
                int total = profile.TotalWins + profile.TotalLosses;
                double rate = total > 0 ? (double)profile.TotalWins / total * 100 : 0;

                WinRateLabel.Text = $"אחוז ניצחונות: {rate:F0}%";
                TotalGamesLabel.Text = $"סה\"כ משחקים: {total} | ניצחונות: {profile.TotalWins} | הפסדות: {profile.TotalLosses}";
                WinRateLabel.IsVisible = true;
                TotalGamesLabel.IsVisible = true;

                if (profile.BestWin > 0)
                {
                    BestWinLabel.Text = $"שיא - מספר מהלכים קטן ביותר: {profile.BestWin}";
                    BestWinLabel.IsVisible = true;
                }
            }

            if (games.Any())
            {
                var display = games.Select(g => new GameRecordDisplay
                {
                    WonIcon = g.Won ? "W" : "L",
                    Date = g.Date,
                    ShotsText = $"{g.Shots} יריות",
                    AccuracyText = $"{g.Accuracy:F0}%"
                }).ToList();

                HistoryList.ItemsSource = new ObservableCollection<GameRecordDisplay>(display);
            }
            else
            {
                HistorySection.IsVisible = false;
            }
        }

        private async void NewGame_Click(object sender, EventArgs e)
        {
            var newModePage = new GameModePage(_player, _firebase);
            await Navigation.PushAsync(newModePage);

            var navStack = Navigation.NavigationStack.ToList();
            foreach (var page in navStack.Skip(1).Take(navStack.Count - 2))
                Navigation.RemovePage(page);
        }

        private async void MainMenu_Click(object sender, EventArgs e)
        {
            await Navigation.PopToRootAsync();
        }
    }
}