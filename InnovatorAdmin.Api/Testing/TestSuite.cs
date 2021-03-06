﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InnovatorAdmin.Testing
{
  public class TestSuite
  {
    private List<ICommand> _init = new List<ICommand>();
    private List<ICommand> _cleanup = new List<ICommand>();
    private List<ParamAssign> _output = new List<ParamAssign>();
    private List<TestRun> _results = new List<TestRun>();
    private List<Test> _tests = new List<Test>();

    public IList<ICommand> Cleanup { get { return _cleanup; } }
    public string Comment { get; set; }
    public IList<ICommand> Init { get { return _init; } }
    public IEnumerable<ParamAssign> Output { get { return _output; } }
    public IEnumerable<TestRun> Results { get { return _results; } }
    public IList<Test> Tests { get { return _tests; } }

    public async Task Run(TestContext context)
    {
      _results.Clear();
      _output.Clear();

      var start = DateTime.Now;
      var i = 0;
      try
      {
        for (i = 0; i < _init.Count; i++)
        {
          await _init[i].Run(context);
        }
      }
      catch (Exception ex)
      {
        _results.Add(new TestRun()
        {
          Name = "* Init",
          Result = TestResult.Fail,
          Start = start,
          ErrorLine = i + 1,
          Message = ex.Message
        });
        return;
      }

      foreach (var test in _tests)
      {
        _results.Add(await test.Run(context));
      }

      try
      {
        for (i = 0; i < _cleanup.Count; i++)
        {
          await _cleanup[i].Run(context);
        }
      }
      catch (Exception ex)
      {
        _results.Add(new TestRun()
        {
          Name = "* Cleanup",
          Result = TestResult.Fail,
          Start = start,
          ErrorLine = i + 1,
          Message = ex.Message
        });
        return;
      }

      foreach (var kvp in context.Parameters)
      {
        _output.Add(new ParamAssign()
        {
          Name = kvp.Key,
          Value = kvp.Value
        });
      }
    }
  }
}
