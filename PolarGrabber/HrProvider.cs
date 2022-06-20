using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace PolarGrabber
{
    public class HrProvider
    {
        DeviceInformation device;
        GattDeviceService service;
        GattCharacteristic characteristic;

        public bool deviceFound;
        public string deviceName;
        public bool isRunning;

        // when hr value is processed from the device
        public event EventHandler<HrEventArgs> HrTaken;

        protected void OnHrTaken(HrData hr)
        {
            HrTaken?.Invoke(this, new HrEventArgs(hr));
        }

        public async Task FindDevice()
        {
            // finds the first device that supports GATT HR
            deviceFound = false;

            string filter = GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.HeartRate);
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(filter);

            if (devices.Count > 0)
            {
                device = devices[0];
                deviceName = device.Name;
                deviceFound = true;
            }
        }

        public async Task Start()
        {
            isRunning = false;

            if (device != null)
            {
                // if a device was found

                service = await GattDeviceService.FromIdAsync(device.Id);
                if (service != null)
                {
                    // create service representing the device
                    // this often fails if the device was not properly closed last time. just wait and retry.

                    GattCharacteristicsResult devchar = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.HeartRateMeasurement);
                    if (devchar.Status == GattCommunicationStatus.Success)
                    {
                        // use the first characteristic within HR service
                        characteristic = devchar.Characteristics[0];

                        // attempt encryption
                        characteristic.ProtectionLevel = GattProtectionLevel.EncryptionRequired;
                        characteristic.ValueChanged += HrReceived;

                        GattReadClientCharacteristicConfigurationDescriptorResult desresult = await characteristic.ReadClientCharacteristicConfigurationDescriptorAsync();
                        if (desresult.Status != GattCommunicationStatus.Success || desresult.ClientCharacteristicConfigurationDescriptor != GattClientCharacteristicConfigurationDescriptorValue.Notify)
                        {
                            GattCommunicationStatus cstat = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);

                            if (cstat == GattCommunicationStatus.Success)
                                isRunning = true;

                        }
                        else
                        {
                            isRunning = true;
                        }

                    }
                }
            }
        }

        public void Stop()
        {
            // stop everything properly
            if (characteristic != null)
            {
                characteristic.ValueChanged -= HrReceived;
                characteristic = null;
            }

            if (service != null)
            {
                service.Dispose();
                service = null;
            }

            isRunning = false;
        }

        private void HrReceived(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // this gets fired everytime there is HR data
            byte[] data = new byte[args.CharacteristicValue.Length];

            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);

            HrData parsed = new HrData(data);

            OnHrTaken(parsed);
        }

    }

    [Flags]
    public enum HrFlags
    {
        // bit mask constants from the gatt hr standard
        None = 0,
        HeartRateValueUINT16 = 1,
        SensorContactStatus = 2,
        SensorContactSupported = 4,
        EnergyExpendedPresent = 8,
        RRIntervalsPresent = 16
    }

    public class HrData
    {
        public HrFlags Flags;
        public ushort HrValue;
        public ushort EnergyExpended;
        public List<ushort> RRIntervals;

        public HrData(byte[] data)
        {
            int offset = 0;

            Flags = (HrFlags)data[offset];
            offset++;

            // determine hr data structure length
            int HrLen = ((Flags & HrFlags.HeartRateValueUINT16) == HrFlags.HeartRateValueUINT16) ? 2 : 1;
            
            if (HrLen == 2)
            {
                // uint16
                HrValue = BitConverter.ToUInt16(data, offset);
            }
            else
            {
                // byte
                HrValue = data[offset];
            }
            offset += HrLen;

            // determine energy presence
            if ((Flags & HrFlags.EnergyExpendedPresent) == HrFlags.EnergyExpendedPresent)
            {
                EnergyExpended = BitConverter.ToUInt16(data, offset);
                offset += 2;
            }

            // determine hr intervals presence
            if ((Flags & HrFlags.RRIntervalsPresent) == HrFlags.RRIntervalsPresent)
            {
                RRIntervals = new List<ushort>();
                while (offset < data.Length)
                {
                    // read out the rest of the data to the intervals list
                    ushort rr = BitConverter.ToUInt16(data, offset);
                    RRIntervals.Add(rr);
                    offset += 2;
                }
            }


        }

    }




    public class HrEventArgs : EventArgs
    {
        public HrData HrData { get; }
        public HrEventArgs(HrData newData)
        {
            HrData = newData;
        }
    }
}
