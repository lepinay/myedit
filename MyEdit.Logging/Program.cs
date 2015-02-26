using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// https://msdn.microsoft.com/en-us/library/system.diagnostics.tracing.eventsource.aspx
namespace MyEdit.Logging
{


    class Program
    {
        static void Main(string[] args)
        {
            using (var session = new TraceEventSession("MyRealTimeSession2"))         // Create a session to listen for events
            {
                session.Source.Dynamic.All += delegate(TraceEvent data)              // Set Source (stream of events) from session.  
                {                                                                    // Get dynamic parser (knows about EventSources) 
                    // Subscribe to all EventSource events
                    Console.WriteLine(data.EventName);                          // Print each message as it comes in 
                };

                var eventSourceGuid = TraceEventProviders.GetEventSourceGuidFromName("MyEdit2"); // Get the unique ID for the eventSouce. 
                session.EnableProvider(eventSourceGuid);                                               // Enable MyEventSource.
                session.Source.Process();                                                              // Wait for incoming events (forever).  
            }
        }
    }
}
