# Parakeet
A BLE GATT client to read the Heart Rate from smart devices
Thank you for using Parakeet

<b>The app has been tested with:</b>
- Pebble 2 HR
- Amazfit Bip

The app won't work with:
- Most fitbit devices
- Apple watch
- Wear OS devices (Android)

<b>The app is likely to work with:</b>
- Polar heart rate chest band,
- Most Bluetooth Low Energy devices with an integrated GATT server

Visual studio will take care of most problems by downloading a ton of libraries, but you have to manually activate the bluetooth component from the project settings

<b>How to install:</b>

1. Install the certificate, selecting local machine and Third party Root
2. Install the app
3. In case the installer asks for again for the certificate, install it in other different paths

<b>How to use Parakeet:</b>

1. Connect your Bluetooth device to your pc using the windows settings
2. Launch parakeet
3. If your device has a continuos heart rate function or a "fitness" mode, activate it
4. Select your device from the in-app device list
5. You can choose a TCP port to send the heart rate values, the address is always localhost
6. Click connect and wait for the values

<b>Things you should know:</b>
- This is a spike solution for a problem encountered during the development of a bigger university project
- The reset button doesn't always work, don't use it pls
- The code is well commented
