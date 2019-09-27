﻿using DuetAPI.Utility;
using System;
using System.Collections.ObjectModel;

namespace DuetAPI.Machine
{
    /// <summary>
    /// Representation of the full machine model as maintained by DCS
    /// </summary>
    public sealed class MachineModel : IAssignable, ICloneable
    {
        /// <summary>
        /// Information about every available G/M/T-code channel
        /// </summary>
        public Channels Channels { get; private set; } = new Channels();

        /// <summary>
        /// Information about the main and expansion boards
        /// </summary>
        public Electronics Electronics { get; private set; } = new Electronics();

        /// <summary>
        /// List of configured fans
        /// </summary>
        /// <seealso cref="Fan"/>
        public ObservableCollection<Fan> Fans { get; } = new ObservableCollection<Fan>();

        /// <summary>
        /// Information about the heat subsystem
        /// </summary>
        public Heat Heat { get; private set; } = new Heat();

        /// <summary>
        /// Information about the current file job (if any)
        /// </summary>
        public Job Job { get; private set; } = new Job();

        /// <summary>
        /// List of configured laser diodes
        /// </summary>
        public ObservableCollection<Laser> Lasers { get; } = new ObservableCollection<Laser>();
        
        /// <summary>
        /// Information about message box requests
        /// </summary>
        public MessageBox MessageBox { get; private set; } = new MessageBox();
        
        /// <summary>
        /// Generic messages that do not belong explicitly to codes being executed.
        /// This includes status messages, generic errors and outputs generated by M118
        /// </summary>
        /// <seealso cref="Message"/>
        [JsonGrowingList]
        public ObservableCollection<Message> Messages { get; } = new ObservableCollection<Message>();
        
        /// <summary>
        /// Information about the move subsystem
        /// </summary>
        public Move Move { get; private set; } = new Move();
        
        /// <summary>
        /// Information about connected network adapters
        /// </summary>
        public Network Network { get; private set; } = new Network();
        
        /// <summary>
        /// Information about the 3D scanner subsystem
        /// </summary>
        public Scanner Scanner { get; private set; } = new Scanner();
        
        /// <summary>
        /// Information about connected sensors including Z-probes and endstops
        /// </summary>
        public Sensors Sensors { get; private set; } = new Sensors();
        
        /// <summary>
        /// List of configured CNC spindles
        /// </summary>
        /// <seealso cref="Spindle"/>
        public ObservableCollection<Spindle> Spindles { get; } = new ObservableCollection<Spindle>();
        
        /// <summary>
        /// Information about the machine state
        /// </summary>
        public State State { get; private set; } = new State();
        
        /// <summary>
        /// List of configured storage devices
        /// </summary>
        /// <seealso cref="Storage"/>
        public ObservableCollection<Storage> Storages { get; } = new ObservableCollection<Storage>();
        
        /// <summary>
        /// List of configured tools
        /// </summary>
        /// <seealso cref="Tool"/>
        public ObservableCollection<Tool> Tools { get; } = new ObservableCollection<Tool>();

        /// <summary>
        /// List of user-defined variables
        /// </summary>
        /// <seealso cref="UserVariable"/>
        public ObservableCollection<UserVariable> UserVariables { get; } = new ObservableCollection<UserVariable>();

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
            if (!(from is MachineModel other))
            {
                throw new ArgumentException("Invalid type");
            }

            Channels.Assign(other.Channels);
            Electronics.Assign(other.Electronics);
            ListHelpers.AssignList(Fans, other.Fans);
            Heat.Assign(other.Heat);
            Job.Assign(other.Job);
            ListHelpers.AssignList(Lasers, other.Lasers);
            MessageBox.Assign(other.MessageBox);
            ListHelpers.AssignList(Messages, other.Messages);
            Move.Assign(other.Move);
            Network.Assign(other.Network);
            Scanner.Assign(other.Scanner);
            Sensors.Assign(other.Sensors);
            ListHelpers.AssignList(Spindles, other.Spindles);
            State.Assign(other.State);
            ListHelpers.AssignList(Storages, other.Storages);
            ListHelpers.AssignList(Tools, other.Tools);
            ListHelpers.AssignList(UserVariables, other.UserVariables);
        }

        /// <summary>
        /// Creates a clone of this instance
        /// </summary>
        /// <returns>Clone of this instance</returns>
        public object Clone()
        {
            MachineModel clone = new MachineModel
            {
                Channels = (Channels)Channels.Clone(),
                Electronics = (Electronics)Electronics.Clone(),
                Heat = (Heat)Heat.Clone(),
                Job = (Job)Job.Clone(),
                MessageBox = (MessageBox)MessageBox.Clone(),
                Move = (Move)Move.Clone(),
                Network = (Network)Network.Clone(),
                Scanner = (Scanner)Scanner.Clone(),
                Sensors = (Sensors)Sensors.Clone(),
                State = (State)State.Clone()
            };

            ListHelpers.CloneItems(clone.Fans, Fans);
            ListHelpers.CloneItems(clone.Lasers, Lasers);
            ListHelpers.CloneItems(clone.Messages, Messages);
            ListHelpers.CloneItems(clone.Spindles, Spindles);
            ListHelpers.CloneItems(clone.Storages, Storages);
            ListHelpers.CloneItems(clone.Tools, Tools);
            ListHelpers.CloneItems(clone.UserVariables, UserVariables);

            return clone;
        }
    }
}