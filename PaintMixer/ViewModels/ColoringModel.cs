using System.ComponentModel.DataAnnotations;
using System.Net.NetworkInformation;

namespace PaintMixer.ViewModels
{
    public class ColoringModel
    {
        int red, green, blue, black, white, yellow;

        [Required, Range(0,100, ErrorMessage = "{0} color must be a number between {1} and {2}")]
        public int Red { 
            get { return red; } 
            set {         
                red = value;
                updateTotal();
            }
        }

        [Required, Range(0, 100, ErrorMessage = "{0} color must be a number between {1} and {2}")]
        public int Black
        {
            get { return black; }
            set
            {
                black = value;
                updateTotal();
            }
        }

        [Required, Range(0, 100, ErrorMessage = "{0} color must be a number between {1} and {2}")]
        public int White
        {
            get { return white; }
            set
            {
                white = value;
                updateTotal();
            }
        }

        [Required, Range(0, 100, ErrorMessage = "{0} color must be a number between {1} and {2}")]
        public int Yellow
        {
            get { return yellow; }
            set
            {
                yellow = value;
                updateTotal();
            }
        }

        [Required, Range(0, 100, ErrorMessage = "{0} color must be a number between {1} and {2}")]
        public int Blue
        {
            get { return blue; }
            set
            {
                blue = value;
                updateTotal();
            }
        }

        [Required, Range(0, 100, ErrorMessage = "{0} color must be a number between {1} and {2}")]
        public int Green
        {
            get { return green; }
            set
            {
                green = value;
                updateTotal();
            }
        }        

        [Range(1, 100, ErrorMessage = "Total dye amounts must sum between {1}% and {2}%")]
        public int Total { get;  set; } = 0;

        private void updateTotal()         {
            Total = red + black + white + yellow + blue + green;
        }

    }
}
