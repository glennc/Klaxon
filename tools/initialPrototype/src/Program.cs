using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace dotnet_now
{
    class Program
    {
        static void Main(string[] args)
        {
            var workingDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var projectName = workingDir.Name;

            if(args.Any())
            {
                if(args[0] == "startup")
                {
                    Console.WriteLine("Generating Startup.cs to get started with...");
                    Execute("mv", ".net/Startup.cs ./Startup.cs");
                }
                else if(args[0] == "main")
                {
                    Console.WriteLine("Generating Program.cs to get started with..");
                    Execute("mv", ".net/Program.cs ./Program.cs");
                }
            }
            else
            {
                Console.WriteLine("Starting .NET.");
                //TODO: one of the many things this doesn't handle well is not being on the internet (restore failure)
                if(!Directory.Exists(".net"))
                {
                    Console.WriteLine("Creating .NET API project");
                    File.WriteAllText($"./{projectName}.cs", FileContent.AppCodeTemplate.Replace("TOKEN", projectName));
                    File.WriteAllText($"./{projectName}.csproj", FileContent.Csproj.Replace("TOKEN", projectName));
                    Execute("mkdir", ".net");
                    Execute("mkdir", ".net/obj");
                    File.WriteAllText($".net/obj/{projectName}.csproj.dotnetwatch.g.targets", FileContent.WatcherTargets);
                    Console.WriteLine("Restoring packages...");
                    Execute("dotnet", "restore");
                }

                if(!File.Exists(".net/Program.cs") && !File.Exists("Program.cs"))
                {
                    File.WriteAllText(".net/Program.cs", FileContent.Program.Replace("TOKEN", projectName));
                }

                if(!File.Exists(".net/Startup.cs") && !File.Exists("Startup.cs"))
                {
                    File.WriteAllText(".net/Startup.cs", FileContent.Startup.Replace("TOKEN", projectName));
                }

                var outputString = new StringBuilder();
                var p = new Process();
                //TODO: Once a build of preview2 is available I can pass the arg to watch that sets the obj directory.
                //This will stop the obj folder from appearing at the top level all the time.
                p.StartInfo = new ProcessStartInfo( "dotnet", "watch run" ) 
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };
                p.OutputDataReceived += new DataReceivedEventHandler((sender, e) => 
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        outputString.AppendLine(e.Data);
                    }
                });
                p.Start();
                p.BeginOutputReadLine();

                var run = true;
                var started = false;
                Uri listeningUri = null;

                Console.Write("Compiling app and Launching server.");

                do
                {
                    Console.Write(".");
                    var output = outputString.ToString();
                    started = output.Contains("Now listening on:");
                    if(started)
                    {
                        var groups = Regex.Match(output, "Now listening on: (.+)", RegexOptions.IgnoreCase).Groups;
                        listeningUri = new Uri(groups[1].Value);
                    }
                    System.Threading.Thread.Sleep(2000);
                }
                while(!started);

                Console.WriteLine(".");
                Console.WriteLine("Listening on: " + listeningUri.ToString());

                var paths = new Stack<string>();

                do
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Run '!logs' to see server logs. '!exit' exits.");
                    Console.ForegroundColor = ConsoleColor.White;

                    var fullBaseUri = listeningUri.ToString();
                    foreach(var path in paths)
                    {
                        fullBaseUri = fullBaseUri + path;
                    }

                    Console.Write(fullBaseUri + ">");

                    var command = Console.ReadLine();
                    switch(command)
                    {
                        //TODO: ! is an awkward command identifier. Need to change that.
                        case "!logs":
                            if(outputString.Length == 0)
                            {
                                Console.WriteLine("There are no more logs.");
                            }
                            else
                            {
                                Console.WriteLine("Retieving server logs...");
                                Console.WriteLine(outputString.ToString());
                                outputString.Clear();
                            }
                        break;
                        case "!exit":
                            Console.WriteLine("Shutting down server.");
                            p.StandardInput.WriteLine(3.ToString("X"));
                            run = false;
                            break;
                        case "!clear":
                            Execute("clear");
                            break;
                        default:

                            if(command.StartsWith("!cd"))
                            {
                                Console.WriteLine("Pushing new path");
                                var path = command.TrimStart("!cd".ToCharArray());
                                path = path.Trim();

                                if(path == "..")
                                {
                                    paths.Pop();
                                    break;
                                }

                                if(!path.EndsWith("/"))
                                {
                                    path = path + "/";
                                }
                                paths.Push(path);
                                break;
                            }

                            outputString.Clear();
                            var requestUrl = new Uri(new Uri(fullBaseUri), command).ToString();
                            Curl(requestUrl);
                        break;
                    }
                }
                while(run);
            }
        }

        private static void Curl(string args) => Execute("curl", args);

        private static void Execute(string command, string args = "")
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo( command, args ) 
                {
                    UseShellExecute = false
                };

            p.Start();
            p.WaitForExit();
        }
    }


}
