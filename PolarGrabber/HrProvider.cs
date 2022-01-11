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

        protected void OnHrTaken(int hr)
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
                        characteristic.ValueChanged += HrChanged;

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
                characteristic.ValueChanged -= HrChanged;
                characteristic = null;
            }

            if (service != null)
            {
                service.Dispose();
                service = null;
            }

            isRunning = false;
        }

        private void HrChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // this gets fired everytime there is HR data
            byte[] data = new byte[args.CharacteristicValue.Length];

            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);

            ParseData(data);
        }

        private void ParseData(byte[] data)
        {
            // ripped from some BLE HR example
            // i know it could be simplified, but maybe there is some more use to the offset?

            byte offset = 0;
            byte flags = data[offset];
            bool isHRLong = (flags & 0x01) != 0;

            offset++;

            ushort HRval = 0;
            if (isHRLong)
            {
                HRval = (ushort)((data[offset + 1] << 8) + data[offset]);
                offset += 2;
            }
            else
            {
                HRval = data[offset];
                offset++;
            }

            OnHrTaken(HRval);

        }


    }

    public class HrEventArgs : EventArgs
    {
        public int HrValue { get; }
        public HrEventArgs(int Hr)
        {
            HrValue = Hr;
        }
    }
}
