Structures: 

  // Test adapter and array of sources map:
    // { 
    //   C:\temp\testAdapter1.dll : [ source1.dll, source2.dll ], 
    //   C:\temp\testadapter2.dll : [ source3.dll, source2.dll ]
    // }
    public Dictionary<string, IEnumerable<string>> AdapterSourceMap 




TimeSpan

string? RunSettings 


string? TestCaseFilter 

TestSessionInfo? TestSessionInfo 

TestPlatformOptions



parameters to start additional executables should be part of the spec, because the are "requests", and a way for additional tools to integrate with us.
