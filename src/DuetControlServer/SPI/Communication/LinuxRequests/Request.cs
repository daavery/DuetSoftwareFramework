namespace DuetControlServer.SPI.Communication.LinuxRequests
{
    /// <summary>
    /// Request indices for SPI transfers from the Linux board to the RepRapFirmware controller
    /// </summary>
    public enum Request : ushort
    {
        /// <summary>
        /// Request state of the GCode buffers
        /// </summary>
        GetState = 0,

        /// <summary>
        /// Perform an immediate emergency stop
        /// </summary>
        EmergencyStop = 1,
        
        /// <summary>
        /// Reset the controller
        /// </summary>
        Reset = 2,
        
        /// <summary>
        /// Request the execution of a G/M/T-code
        /// </summary>
        /// <seealso cref="CodeHeader"/>
        Code = 3,

        /// <summary>
        /// Request a part of the machine's object model
        /// </summary>
        /// <seealso cref="SharedRequests.ObjectModel"/>
        GetObjectModel = 4,
        
        /// <summary>
        /// Set a value in the machine's object model (reserved)
        /// </summary>
        /// <seealso cref="LinuxRequests.SetObjectModel"/>
        SetObjectModel = 5,
        
        /// <summary>
        /// Tell the firmware a print has started and set information about the file being processed
        /// </summary>
        /// <seealso cref="LinuxRequests.PrintStarted"/>
        PrintStarted = 6,
        
        /// <summary>
        /// Tell the firmware a print has been stopped and reset information about the file being processed
        /// </summary>
        /// <seealso cref="LinuxRequests.PrintStopped"/>
        PrintStopped = 7,
        
        /// <summary>
        /// Notify the firmware about the completion of a macro file
        /// </summary>
        /// <seealso cref="LinuxRequests.MacroCompleted"/>
        MacroCompleted = 8,
        
        /// <summary>
        /// Request the heightmap coordinates as generated by G29
        /// </summary>
        GetHeightMap = 9,

        /// <summary>
        /// Lock movement and wait for standstill
        /// </summary>
        LockMovementAndWaitForStandstill = 10,

        /// <summary>
        /// Unlock everything again
        /// </summary>
        UnlockAll = 11
    }
}