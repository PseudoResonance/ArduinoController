using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArduinoController
{
    public class Data : INotifyPropertyChanged
    {
        private int _deadzoneCount = Main.deadzoneRadius;
        public int DeadzoneCount
        {
            get { return _deadzoneCount; }
            set
            {
                if (_deadzoneCount != value)
                {
                    _deadzoneCount = value;
                    Main.deadzoneRadius = (short)_deadzoneCount;
                    OnPropertyChanged("DeadzoneCount");
                }
            }
        }

        private int _updateCount = Main.updateDelay;
        public int UpdateCount
        {
            get { return _updateCount; }
            set
            {
                if (_updateCount != value)
                {
                    _updateCount = value;
                    Main.updateDelay = (short)_updateCount;
                    OnPropertyChanged("UpdateCount");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
    }
}
