using System;
using Microsoft.AspNetCore.Mvc;

public class demoappController
{
    [HttpGet("/")]
    public string Hello() => "Hello.";
}