//using Microsoft.Xaml.Behaviors;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Documents;
//using System.Windows.Media;

//namespace ADLMRateGen.Helpers
//{
//	public class TextBoxCueBannerBehavior : Behavior<TextBox>
//	{
//		public static readonly DependencyProperty CueProperty =
//		  DependencyProperty.Register(
//			  "Cue", typeof(string), typeof(TextBoxCueBannerBehavior));

//		public string Cue
//		{
//			get => (string)GetValue(CueProperty);
//			set => SetValue(CueProperty, value);
//		}

//		private AdornerLayer _layer;
//		private CueBannerAdorner _adorner;

//		protected override void OnAttached()
//		{
//			base.OnAttached();
//			AssociatedObject.Loaded += OnLoaded;
//			AssociatedObject.TextChanged += OnTextChanged;
//		}

//		private void OnLoaded(object s, RoutedEventArgs e)
//		{
//			_layer = AdornerLayer.GetAdornerLayer(AssociatedObject);
//			_adorner = new CueBannerAdorner(AssociatedObject, Cue);
//			ShowOrHide();
//		}

//		private void OnTextChanged(object s, TextChangedEventArgs e) => ShowOrHide();

//		private void ShowOrHide()
//		{
//			if (_layer == null) return;
//			if (string.IsNullOrEmpty(AssociatedObject.Text))
//				_layer.Add(_adorner);
//			else
//				_layer.Remove(_adorner);
//		}
//	}

//	// The Adorner class itself just draws your Cue string lightly
//	// ... you can find plenty of examples of CueBannerAdorner online.
//}
