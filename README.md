# PolarGrabber
Getting real-time HR data from Polar H10+ and publishing them via javascript server-side event.
I assume it will work with any GATT HR device, not just Polar, but it is what I use it with.

This thing will search for the devices supporting GATT HR measurement, select the first one returned by Windows (so if you require working with multiple, you have to rewrite that portion) and start the data gathering.

It will also set up a primitive HTTP server on the predefined url and keep track of all the clients connected to that endpoint. The clients can then subscribe to listen to server-side javascript events and the HTTP server will send an event each time there is a new HR reading.

The supplied htm file is an example that charts the HR reading into a (up to) 10 minutes long chart.

Additionally, the program also creates a text file where it writes all the readings prefixed with current date and time for further analysis.

It is a command line application and will output some symbols:
+ symbol = new listener connected, - symbol = listener disconnected (which is detected only when trying to send HR data, not sooner given how the nature of HttpListener works in .NET), heart symbol - HR data received and processed

Press Enter to gracefully stop the app. If you hard-terminate the app, it will block the BT device for couple of seconds before it can be used again.
