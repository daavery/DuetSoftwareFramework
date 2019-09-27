﻿using DuetAPI.Utility;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DuetAPI.Machine
{
    /// <summary>
    /// Information about the electronics used
    /// </summary>
    public sealed class Electronics : IAssignable, ICloneable, INotifyPropertyChanged
    {
        /// <summary>
        /// Event to trigger when a property has changed
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Version of the Duet Software Framework package
        /// </summary>
        public string Version
        {
            get => _version;
            set
            {
                if (_version != value)
                {
                    _version = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private string _version;

        /// <summary>
        /// Type name of the main board
        /// </summary>
        public string Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private string _type;
        
        /// <summary>
        /// Short code name of the board
        /// </summary>
        public string ShortName
        {
            get => _shortName;
            set
            {
                if (_shortName != value)
                {
                    _shortName = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private string _shortName;

        /// <summary>
        /// Full name of the main board
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private string _name;
        
        /// <summary>
        /// Revision of the main board
        /// </summary>
        public string Revision
        {
            get => _revision;
            set
            {
                if (_revision != value)
                {
                    _revision = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private string _revision;
        
        /// <summary>
        /// Main firmware of the attached main board
        /// </summary>
        public Firmware Firmware { get; private set; } = new Firmware();
        
        /// <summary>
        /// Processor ID of the main board
        /// </summary>
        public string ProcessorID
        {
            get => _processorID;
            set
            {
                if (_processorID != value)
                {
                    _processorID = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private string _processorID;
        
        /// <summary>
        /// Input voltage details of the main board (in V or null if unknown)
        /// </summary>
        public MinMaxCurrent<float?> VIn { get; private set; } = new MinMaxCurrent<float?>();
        
        /// <summary>
        /// MCU temperature details of the main board (in degC or null if unknown)
        /// </summary>
        public MinMaxCurrent<float?> McuTemp { get; private set; } = new MinMaxCurrent<float?>();
        
        /// <summary>
        /// Information about attached expansion boards
        /// </summary>
        ///<seealso cref="ExpansionBoard"/>
        public ObservableCollection<ExpansionBoard> ExpansionBoards { get; } = new ObservableCollection<ExpansionBoard>();

        /// <summary>
        /// Assigns every property from another instance
        /// </summary>
        /// <param name="from">Object to assign from</param>
        /// <exception cref="ArgumentNullException">other is null</exception>
        /// <exception cref="ArgumentException">Types do not match</exception>
        public void Assign(object from)
        {
            if (from == null)
            {
                throw new ArgumentNullException();
            }
            if (!(from is Electronics other))
            {
                throw new ArgumentException("Invalid type");
            }

            Version = other.Version;
            ShortName = other.ShortName;
            Name = other.Name;
            Type = other.Type;
            Name = other.Name;
            Revision = other.Revision;
            Firmware.Assign(other.Firmware);
            ProcessorID = other.ProcessorID;
            VIn.Assign(other.VIn);
            McuTemp.Assign(other.McuTemp);
            ListHelpers.AssignList(ExpansionBoards, other.ExpansionBoards);
        }

        /// <summary>
        /// Creates a clone of this instance
        /// </summary>
        /// <returns>A clone of this instance</returns>
        public object Clone()
        {
            Electronics clone = new Electronics
            {
                Version= Version,
                Type = Type,
                Name = Name,
                ShortName = ShortName,
                Revision = Revision,
                Firmware = (Firmware)Firmware.Clone(),
                ProcessorID = ProcessorID,
                VIn = (MinMaxCurrent<float?>)VIn.Clone(),
                McuTemp = (MinMaxCurrent<float?>)McuTemp.Clone()
            };

            ListHelpers.CloneItems(clone.ExpansionBoards, ExpansionBoards);

            return clone;
        }
    }
}
