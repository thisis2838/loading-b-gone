state ("hl2")
{

}
state ("bms")
{

}

startup
{
    refreshRate = 100;
    vars.TimerModel = new TimerModel { CurrentState = timer };

    settings.Add("newFile", true, "Create a new Timestamp file per run.");
    settings.SetToolTip("newFile", 
        "If true, this ASL will generate a new file per run start named according this format: \n" + 
        "'timestamp on <time of run start>.txt'. Otherwise, it will only write to a 'timestamp.txt' file.");

    settings.Add("info1", true, "Source Loading-B-Gone Companion ASL");
    settings.Add("info1-1", false, "Timestamp files will be put in the folder 'loading-b-gone_timestamps' next to Livesplit's .exe file", "info1");
    settings.Add("info1-2", false, "Remember to fill in when in the video the run starts in those files!", "info1");

    if (!Directory.Exists("loading-b-gone_timestamps"))
        Directory.CreateDirectory("loading-b-gone_timestamps");

    vars.Ready = false;
}

init
{
    vars.Ready = false;

#region SIGSCANNING FUNCTIONS
    print("Game process found");
    
    print("Game main module size is " + modules.First().ModuleMemorySize.ToString());

    Func<string, ProcessModuleWow64Safe> GetModule = (moduleName) =>
    {
        return modules.FirstOrDefault(x => x.ModuleName.ToLower() == moduleName);
    };

    Func<uint, string> GetByteStringU = (o) =>
    {
        return BitConverter.ToString(BitConverter.GetBytes(o)).Replace("-", " ");
    };

    Func<string, string> GetByteStringS = (o) =>
    {
        string output = "";
        foreach (char i in o)
            output += ((byte)i).ToString("x2") + " ";

        return output;
    };

    Func<string, SignatureScanner> GetSignatureScanner = (moduleName) =>
    {
        ProcessModuleWow64Safe proc = GetModule(moduleName);
        Thread.Sleep(1000);
        if (proc == null)
            throw new Exception(moduleName + " isn't loaded!");
        print("Module " + moduleName + " found at 0x" + proc.BaseAddress.ToString("X"));
        return new SignatureScanner(game, proc.BaseAddress, proc.ModuleMemorySize);
    };

    Func<SignatureScanner, uint, bool> IsWithinModule = (f_scanner, ptr) =>
    {
        uint nPtr = (uint)ptr;
        uint nStart = (uint)f_scanner.Address;
        return ((nPtr > nStart) && (nPtr < nStart + f_scanner.Size));
    };

    Func<SignatureScanner, uint, bool> IsLocWithinModule = (f_scanner, ptr) =>
    {
        uint nPtr = (uint)ptr;
        return ((nPtr % 4 == 0) && IsWithinModule(f_scanner, ptr));
    };

    Action<IntPtr, string> ReportPointer = (ptr, name) => 
    {
        if (ptr == IntPtr.Zero)
            print(name + " ptr was NOT found!!");
        else
            print(name + " ptr was found at 0x" + ptr.ToString("X"));
    };

    // throw an exception if given pointer is null
    Action<IntPtr, string> ShortOut = (ptr, name) =>
    {
        if (ptr == IntPtr.Zero)
        {
            Thread.Sleep(1000);
            throw new Exception(name + " ptr was NOT found!!");
        }
    };

    Func<IntPtr, int, int, IntPtr> ReadRelativeReference = (ptr, trgOperandOffset, totalSize) =>
    {
        int offset = memory.ReadValue<int>(ptr + trgOperandOffset, 4);
        if (offset == 0)
            return IntPtr.Zero; 
        IntPtr actualPtr = IntPtr.Add((ptr + totalSize), offset);
        return actualPtr;
    };
#endregion

#region SIGSCANNING
    var engine = GetModule("engine.dll");
    var scanner = new SignatureScanner(game, engine.BaseAddress, engine.ModuleMemorySize);

    IntPtr loadingPlaqueActivePtr = IntPtr.Zero;
    IntPtr frameCountPtr = IntPtr.Zero;
    try
    {
        // ----- FRAME COUNT -----

        // find target string reference, lives in R_LevelInit()
        SigScanTarget tmpTarg = new SigScanTarget(GetByteStringS("Initializing renderer...\n"));
        IntPtr tmpPtr = scanner.Scan(new SigScanTarget("68" + GetByteStringU((uint)scanner.Scan(tmpTarg))));
        ShortOut(tmpPtr, "frame count - string reference");
        ReportPointer(tmpPtr, "frame count - string reference");

        // find target mov instruction, writes a 1 to our value
        tmpTarg = new SigScanTarget(2, "C7 05 ?? ?? ?? ?? 01 00 00 00");
        frameCountPtr = game.ReadPointer((new SignatureScanner(game, tmpPtr, 0x100)).Scan(tmpTarg));
        ShortOut(frameCountPtr, "frame count");
        ReportPointer(frameCountPtr, "frame count");

        // ----- LOADING PLAQUE ACTIVE -----

        // find target string reference, lives in Host_Error()
        tmpTarg = new SigScanTarget(GetByteStringS("\nHost_Error: %s\n\n"));
        tmpPtr = scanner.Scan(new SigScanTarget("68" + GetByteStringU((uint)scanner.Scan(tmpTarg))));
        ShortOut(tmpPtr, "loading plaque active - string reference");
        ReportPointer(tmpPtr, "loading plaque active - string reference");

        // find target call, lives right above our string reference; from there find target function which is SCR_EndLoadingPlaque()
        IntPtr targFunc = IntPtr.Zero;
        for (int i = 0; i < 0x100; i++)
        {
            byte curByte = 0x0;
            game.ReadValue(tmpPtr - i, out curByte);
            if (curByte == 0xE8)
            {
                IntPtr dest = ReadRelativeReference(tmpPtr - i, 1, 5);
                if (IsLocWithinModule(scanner, (uint)dest))
                {
                    targFunc = dest;
                    break;
                }
            }
        }
        ShortOut(targFunc, "loading plaque active - target function");
        ReportPointer(targFunc, "loading plaque active - target function");

        // find target mov or call which uses our ptr, all inside SCR_EndLoadingPlaque() which we found earlier
        SignatureScanner tmpScanner = new SignatureScanner(game, targFunc, 0x100);
        SigScanTarget targ = new SigScanTarget();
        int tries = 0;
        again:
        switch (tries)
        {
            case 1:
                targ = new SigScanTarget(2, "80 3D ?? ?? ?? ?? 00");
                break;
            case 2:
                targ = new SigScanTarget(1, "A0");
                break;
            case 3:
                throw new Exception("loading plaque active ptr not found");
        }
        targ.OnFound = (f_proc, f_scanner, f_ptr) => 
        {
            uint dest = game.ReadValue<uint>(f_ptr);
            if (IsWithinModule(scanner, (uint)dest))
                return (IntPtr)dest;

            return IntPtr.Zero;
        };
        while ((loadingPlaqueActivePtr = tmpScanner.Scan(targ)) == IntPtr.Zero)
        {
            tries++;
            goto again;
        }
        ShortOut(loadingPlaqueActivePtr, "loading plaque active");
        ReportPointer(loadingPlaqueActivePtr, "loading plaque active");

        vars.Ready = true;
    }
    catch (Exception e)
    {
        print(e.ToString());
    }


#endregion

#region FILE OPERATIONS

    vars.CurFileName = "";

    Action fileRefresh = () => 
    {
        vars.CurFileName = Path.Combine("loading-b-gone_timestamps",
            settings["newFile"] ? 
                "timestamps on " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".txt" : 
                "timestamps.txt");
        File.WriteAllText(vars.CurFileName, 
        "", 
        Encoding.UTF8);
    };

    Action<string> fileWriteLine = (input) => 
    {
        StreamWriter stream = (File.AppendText(vars.CurFileName));
        stream.WriteLine(input);
        stream.Close();
    };

    vars.FileWriteLine = fileWriteLine;
    vars.FileRefresh = fileRefresh;
    vars.FileRefresh();
#endregion


    vars.LoadingPlaqueActive = new MemoryWatcher<bool>(loadingPlaqueActivePtr);
    vars.FrameCount = new MemoryWatcher<int>(new DeepPointer(frameCountPtr));
    vars.LastStartLoadTime = TimeSpan.MinValue;

#region TIME OPERATIONS
    Func<TimeSpan> getCurTime = () =>
    {
        return vars.TimerModel.CurrentState.CurrentTime.RealTime;
    };

    Action<TimeSpan> loadBegan = (time) =>
    {
        vars.LastStartLoadTime = time;
        print(vars.GetCurTime() + ": Load began @ " + vars.LastStartLoadTime + ", frame count = " + vars.FrameCount.Current);
    };

    Action<TimeSpan> loadEnded = (time) =>
    {
        vars.FileWriteLine(vars.LastStartLoadTime.ToString() + "," + time.ToString());
        print(vars.GetCurTime() + ": Load ended @ " + time + ", frame count = " + vars.FrameCount.Current);
        vars.LastStartLoadTime = TimeSpan.MinValue;
    };

    vars.LoadEnded = loadEnded;
    vars.LoadBegan = loadBegan;
    vars.GetCurTime = getCurTime;
	vars.LastCurTime = TimeSpan.MinValue;
#endregion

    
}

update
{
    if (!vars.Ready)
        return;

    vars.LoadingPlaqueActive.Update(game);
    vars.FrameCount.Update(game);

    if (vars.TimerModel.CurrentState.CurrentPhase != TimerPhase.Running)
        return;
	
    if (vars.LoadingPlaqueActive.Current == true && vars.LoadingPlaqueActive.Old == false)
    {
		if (vars.LastStartLoadTime == TimeSpan.MinValue)
			vars.LoadBegan(vars.LastCurTime);
    }
	
	bool doneLoads = 
	(vars.LoadingPlaqueActive.Current == false && vars.FrameCount.Current >= 2);
	
    if ( doneLoads && vars.LastStartLoadTime != TimeSpan.MinValue)
    {
		vars.LoadEnded(vars.GetCurTime());
    }
	
	vars.LastCurTime = vars.GetCurTime();
}

onStart
{
    vars.LastStartLoadTime = TimeSpan.MinValue;
	vars.LastCurTime = vars.GetCurTime();
    vars.FileRefresh();
}