using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Threading;

namespace ParaFactor
{
    /// <summary>
    /// Represents one factor in factorized number.
    /// </summary>
    struct Factor
    {
        /// <summary>
        /// Base of the factor (prime number)
        /// </summary>
        readonly public uint prime;

        /// <summary>
        /// Exponent (power), should be >= 1
        /// </summary>
        readonly public uint exponent;

        public Factor(uint prime, uint exponent = 1)
        {
            this.prime = prime;
            this.exponent = exponent;
        }

        public override string ToString() 
        {
            if (exponent == 0)
            {
                return "1";
            }

            if (exponent == 1)
            {
                return prime.ToString();
            }

            return String.Format("{0}^{1}", prime, exponent);
        }

        /// <summary>
        /// A very crude algorithm for decomposing a number into prime factors.
        /// </summary>
        /// <param name="x">Input number to be decomposed</param>
        /// <returns>Array of factors sorted by the base (ascending order).</returns>
        public static Factor[] factorize(uint x)
        {
            var factors = new List<Factor>();

            uint divisor = 2;
            while (x > 1 && divisor < x)
            {
                uint exponent = 0;
                while (x % divisor == 0)
                {
                    ++exponent;
                    x /= divisor;
                }

                if (exponent > 0)
                {
                    factors.Add(new Factor(divisor, exponent));
                }

                ++divisor;
            }

            if (x > 1)
            {
                factors.Add(new Factor(x));
            }

            return factors.ToArray();
        }
    }

    /// <summary>
    /// Object representing one job (number to be factorized).
    /// It also holds reference to label, where the result should be displayed.
    /// </summary>
    class NumberToFactorize
    {
        public readonly uint number;
        public readonly Label label;
        public Factor[] result;

        public NumberToFactorize(uint number, Label label)
        {
            this.number = number;
            this.label = label;
            result = null;
        }

        public string resultAsString()
        {
            if (result == null || result.Length == 0)
            {
                return "";
            }

            return String.Join(" * ", result.Select(f => f.ToString()));
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private static readonly Regex regexCheckNumbers = new Regex("^[0-9]+$");
        private void CountInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !regexCheckNumbers.IsMatch(e.Text);
        }

        private void CountInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            int res = 0;
            buttonGenerate.IsEnabled = Int32.TryParse(countInput.Text, out res) && res > 1;
        }

        private Label createLabel(uint number)
        {
            var label = new Label();

            label.Content = number.ToString();
            label.Width = 128;
            label.Height = 42;

            label.Margin = new Thickness(2);
            label.HorizontalContentAlignment = HorizontalAlignment.Center;
            label.VerticalContentAlignment = VerticalAlignment.Center;

            label.Background = Brushes.GhostWhite;
            label.BorderBrush = Brushes.LightGray;
            label.BorderThickness = new Thickness(1);

            wrapPanel.Children.Add(label);
            return label;
        }

        private Random generator = new Random();

        /// <summary>
        /// List of all jobs
        /// </summary>
        private List<NumberToFactorize> numbers = new List<NumberToFactorize>();

        private void buttonGenerate_Click(object sender, RoutedEventArgs e)
        {
            wrapPanel.Children.Clear();
            numbers.Clear();

            int count;
            if (Int32.TryParse(countInput.Text, out count))
            {
                while (count-- > 0)
                {
                    uint number = (uint)generator.Next(1000, Int32.MaxValue / 5);
                    var label = createLabel(number);
                    numbers.Add(new NumberToFactorize(number, label));
                }
            }
        }

		private void buttonFactorize_Click(object sender, RoutedEventArgs e)
        {
            foreach (var n in numbers)
            {
                // mark as running
                n.label.Background = Brushes.LightCoral;

                var t1 = Task.Run(() =>
                {
                    // do the job...
                    n.result = Factor.factorize(n.number);
                });
                var t2 = t1.ContinueWith(_ =>
                {
                    // display the result
                    n.label.Content = n.resultAsString();
                    n.label.Background = n.result.Length > 1 || n.result[0].exponent > 1
                        ? Brushes.LightGreen
                        : Brushes.LightYellow;
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }

            // original (slow) solution

            //foreach (var n in numbers)
            //{
            //    // mark as running
            //    n.label.Background = Brushes.LightCoral;

            //    // do the job...
            //    n.result = await Task.Run(() => Factor.factorize(n.number));

            //    // display the result
            //    n.label.Content = n.resultAsString();
            //    n.label.Background = n.result.Length > 1 || n.result[0].exponent > 1 ? Brushes.LightGreen: Brushes.LightYellow;
            //}
        }
	}
}
