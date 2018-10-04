# Parakeet
A BLE GATT client to read the Heart Rate from smart devices
Thank you for using Parakeet

<b>The app has been tested with:</b>
Pebble 2 HR,
Amazfit Bip

<b>The app won't work with:</b>
Most fitbit devices,
Apple watch,
Wear OS devices (Android),

<b>The app is likely to work with:</b>
Polar heart rate chest band,
Most Bluetooth Low Energy devices with an integrated GATT server

Visual studio will take care of most problems by downloading a ton of libraries, but you have to manually activate the bluetooth component from the project settings

<b>How to install:</b>

1. Install the certificate, selecting local machine and Third party Root
2. Install the app
3. In case the installer asks for more certificate, install the certificate in other different paths

<b>How to use Parakeet:</b>

1. connect your Bluetooth device to your pc using the windows settings
2. launch parakeet
3. If your device has a continuos heart rate function or a "fitness" mode, activate it
4. select your device from the in-app device list
5. you can choose a TCP port to send the heart rate values, the address is always localhost
6. click connect and wait for the values

<b>Things you should know:</b>
Things you should know:
- This is a spike solution for a problem encountered during the development of our university project
- The reset button doesn't always work, don't use it pls
- The code is well commented
