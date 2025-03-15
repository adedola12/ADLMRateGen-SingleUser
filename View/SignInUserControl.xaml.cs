using System.Windows;
using System.Windows.Controls;
using ADLMRateGen.Services;
using ADLMRateGen.ViewModel;
using static ADLMRateGen.ViewModel.SignInViewModel;

namespace ADLMRateGen.View
{
	public partial class SignInUserControl : UserControl
	{
		public event EventHandler<LoginEventArgs> LoginSucceeded;

		public SignInUserControl()
		{
			InitializeComponent();

			// Connect to your DB
			var mongoDbService = new MongoDbService(
				"mongodb+srv://dolapo836:[REDACTED]@adlmratedb.zeur8.mongodb.net/?retryWrites=true&w=majority&appName=ADLMRateDB",
				"ADLMRateDB",
				"Users");

			var signInVM = new SignInViewModel(mongoDbService);

			// Bubble up the login event
			signInVM.LoginSucceeded += (sender, args) =>
			{
				// Raise event to let MainWindow know
				LoginSucceeded?.Invoke(this, args);
			};

			DataContext = signInVM;
		}

		private void LoginButton_Click(object sender, RoutedEventArgs e)
		{
			if (DataContext is SignInViewModel vm)
			{
				// Transfer the PasswordBox value to the VM before command
				vm.Password = PasswordBox.Password;
			}
		}
	}
}
