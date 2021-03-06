﻿using DuetAPI.Utility;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DuetAPI.Machine
{
    /// <summary>
    /// Information about an endstop
    /// </summary>
    public sealed class Endstop : IAssignable, ICloneable, INotifyPropertyChanged
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
        /// Action to perform when an endstop is hit
        /// </summary>
        public EndstopAction Action
        {
            get => _action;
            set
            {
                if (_action != value)
                {
                    _action = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private EndstopAction _action;

        /// <summary>
        /// Whether or not the endstop is hit
        /// </summary>
        public bool Triggered
        {
            get => _triggered;
            set
            {
                if (_triggered != value)
                {
                    _triggered = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private bool _triggered;
        
        /// <summary>
        /// Type of the endstop
        /// </summary>
        public EndstopType Type
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
        private EndstopType _type;

        /// <summary>
        /// Index of the used probe (if <see cref="Type"/> is <see cref="EndstopType.Probe"/>)
        /// </summary>
        public int? Probe
        {
            get => _probe;
            set
            {
                if (_probe != value)
                {
                    _probe = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private int? _probe;

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
            if (!(from is Endstop other))
            {
                throw new ArgumentException("Invalid type");
            }

            Action = other.Action;
            Triggered = other.Triggered;
            Type = other.Type;
            Probe = other.Probe;
        }

        /// <summary>
        /// Creates a clone of this instance
        /// </summary>
        /// <returns>A clone of this instance</returns>
        public object Clone()
        {
            return new Endstop
            {
                Action = Action,
                Triggered = Triggered,
                Type = Type,
                Probe = Probe
            };
        }
    }
}