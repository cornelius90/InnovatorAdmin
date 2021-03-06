﻿using InnovatorAdmin;
using ICSharpCode.AvalonEdit.CodeCompletion;
using Innovator.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;

namespace InnovatorAdmin.Editor
{
  public partial class AmlEditorHelper : ISqlMetadataProvider
  {
    private ArasMetadataProvider _metadata;
    protected SqlCompletionHelper _sql;

    private string GetType(XmlReader reader)
    {
      var type = reader.GetAttribute("type");
      if (!string.IsNullOrEmpty(type))
        return type;
      var typeId = reader.GetAttribute("typeId");
      ItemType itemType;
      if (!string.IsNullOrEmpty(typeId) && _metadata != null && _metadata.TypeById(typeId, out itemType))
        return itemType.Name;

      return null;
    }

    public IPromise<CompletionContext> GetCompletions(ITextSource xml, int caret, string soapAction)
    {
      //var overlap = 0;
      if (xml.TextLength < 1) return Promises.Resolved<CompletionContext>(new CompletionContext());

      var path = new List<AmlNode>();
      var existingAttributes = new HashSet<string>();
      Func<string, bool> notExisting = s => !existingAttributes.Contains(s);
      string attrName = null;
      string value = null;
      bool cdata = false;
      var paramNames = new HashSet<string>();

      var state = XmlUtils.ProcessFragment(xml.CreateSnapshot(0, caret), (r, o, st) =>
      {
        switch (r.NodeType)
        {
          case XmlNodeType.Element:
            if (!r.IsEmptyElement)
              path.Add(new AmlNode()
              {
                Offset = o,
                LocalName = r.LocalName,
                Type = GetType(r),
                Action = r.GetAttribute("action"),
                Condition = r.GetAttribute("condition"),
                Id = r.GetAttribute("id")
              });

            existingAttributes.Clear();
            break;
          case XmlNodeType.EndElement:
            path.RemoveAt(path.Count - 1);
            break;
          case XmlNodeType.Attribute:
            existingAttributes.Add(r.LocalName);
            if (st == XmlState.Attribute || st == XmlState.AttributeValue)
              attrName = r.LocalName;
            value = r.Value;
            if (path.Last().LocalName == "Param")
            {
              if (r.LocalName == "name")
              {
                paramNames.Add(r.Value);
              }
            }
            else if (r.Value.StartsWith("@") && r.LocalName != "match")
            {
              paramNames.Add(r.Value.TrimStart('@').TrimEnd('!'));
            }
            break;
          case XmlNodeType.CDATA:
            cdata = true;
            value = r.Value;
            break;
          case XmlNodeType.Text:
            cdata = false;
            value = r.Value;
            if (!string.IsNullOrWhiteSpace(r.Value) && r.Value.Length <= 128 && path.Count > 1
              && path.Last().LocalName != "Item")
            {
              var lastItem = path.LastOrDefault(n => n.LocalName == "Item");
              if (lastItem != null)
              {
                lastItem.Values[path.Last().LocalName] = r.Value;
              }
            }
            if (r.Value.StartsWith("@"))
            {
              paramNames.Add(r.Value.TrimStart('@').TrimEnd('!'));
            }
            break;
        }
        return true;
      });

      // Bail within comments to avoid conflicting with typing
      if (state == XmlState.Comment)
        return Promises.Resolved<CompletionContext>(new CompletionContext());
      if (caret > 0 && state == XmlState.Tag && (xml.GetCharAt(caret - 1) == '"' || xml.GetCharAt(caret - 1) == '\''))
        return Promises.Resolved<CompletionContext>(new CompletionContext() { IsXmlTag = true });

      IPromise<IEnumerable<ICompletionData>> items = null;
      IEnumerable<ICompletionData> appendItems = Enumerable.Empty<ICompletionData>();
      var filter = string.Empty;

      if (path.Count < 1)
      {
        switch (soapAction)
        {
          case "ApplySQL":
            items = Elements("sql");
            break;
          case "ApplyAML":
            items = Elements("AML");
            break;
          case "GetAssignedTasks":
            items = Elements("params");
            break;
          case ArasEditorProxy.UnitTestAction:
            items = Elements("TestSuite");
            break;
          default:
            items = Elements("Item");
            break;
        }
      }
      else
      {
        switch (state)
        {
          case XmlState.Attribute:
          case XmlState.AttributeStart:
            switch (path.Last().LocalName)
            {
              case "and":
              case "or":
              case "not":
              case "Relationships":
              case "AML":
              case "sql":
              case "SQL":
                break;
              case "Path":
                items = Attributes(notExisting, "id");
                break;
              case "Task":
                items = Attributes(notExisting, "id", "completed");
                break;
              case "Variable":
                items = Attributes(notExisting, "id");
                break;
              case "Authentication":
                items = Attributes(notExisting, "mode");
                break;
              case "Param":
                items = Attributes(notExisting, "name", "select");
                break;
              case "Remove":
                items = Attributes(notExisting, "match");
                break;
              case "AssertMatch":
                items = Attributes(notExisting, "match", "removeSysProps");
                break;
              case "Test":
                items = Attributes(notExisting, "name");
                break;
              case "Item":
                switch (soapAction)
                {
                  case "GenerateNewGUIDEx":
                    items = Attributes(notExisting, "quantity");
                    break;
                  case "":
                    break;
                  default:
                    var attributes = new List<string>
                    { "action"
                      , "config_path"
                      , "doGetItem"
                      , "id"
                      , "idlist"
                      , "isCriteria"
                      , "language"
                      , "levels"
                      , "maxRecords"
                      , "page"
                      , "pagesize"
                      , "orderBy"
                      , "queryDate"
                      , "queryType"
                      , "related_expand"
                      , "select"
                      , "serverEvents"
                      , "type"
                      , "typeId"
                      , "version"
                      , "where"};
                    if (path.Last().Action == "getPermissions")
                      attributes.Add("access_type");

                    if (path.Count >= 3
                      && path[path.Count - 2].LocalName == "Relationships"
                      && path[path.Count - 3].LocalName == "Item"
                      && path[path.Count - 3].Action == "GetItemRepeatConfig")
                    {
                      attributes.Add("repeatProp");
                      attributes.Add("repeatTimes");
                    }

                    items = Attributes(notExisting, attributes.ToArray());
                    break;
                }
                break;
              default:
                items = Attributes(notExisting, "condition", "is_null");
                break;
            }

            filter = attrName;
            break;
          case XmlState.AttributeValue:
            if (path.Last().LocalName == "Item")
            {
              ItemType itemType;
              switch (attrName)
              {
                case "action":
                  var baseMethods = new string[] {"ActivateActivity"
                    , "add"
                    , "AddHistory"
                    , "AddItem"
                    , "ApplyUpdate"
                    , "BuildProcessReport"
                    , "CancelWorkflow"
                    , "closeWorkflow"
                    , "copy"
                    , "copyAsIs"
                    , "copyAsNew"
                    , "create"
                    , "delete"
                    , "edit"
                    , "EmailItem"
                    , "EvaluateActivity"
                    , "exportItemType"
                    , "get"
                    , "getAffectedItems"
                    , "getItemAllVersions"
                    , "GetItemConfig"
                    , "getItemLastVersion"
                    , "getItemNextStates"
                    , "getItemRelationships"
                    , "GetItemRepeatConfig"
                    , "getItemWhereUsed"
                    , "GetMappedPath"
                    , "getPermissions"
                    , "getRelatedItem"
                    , "GetUpdateInfo"
                    , "instantiateWorkflow"
                    , "lock"
                    , "merge"
                    , "New Workflow Map"
                    , "promoteItem"
                    , "purge"
                    , "recache"
                    , "replicate"
                    , "resetAllItemsAccess"
                    , "resetItemAccess"
                    , "resetLifecycle"
                    , "setDefaultLifecycle"
                    , "skip"
                    , "startWorkflow"
                    , "unlock"
                    , "update"
                    , "ValidateWorkflowMap"
                    , "version"};

                  var aras = _conn as Innovator.Client.Connection.IArasConnection;
                  var version = aras == null ? -1 : aras.Version;

                  var methods = (IEnumerable<string>)baseMethods;
                  if (version < 10)
                    methods = methods.Concat(Enumerable.Repeat("checkImportedItemType", 1));
                  if (version < 0 || version >= 10)
                    methods = methods.Concat(Enumerable.Repeat("VaultServerEvent", 1));
                  if (version < 0 || version >= 11)
                    methods = methods.Concat(Enumerable.Repeat("GetInheritedServerEvents", 1));

                  items = Promises.Resolved(_metadata.MethodNames.Select(m => (ICompletionData)new AttributeValueCompletionData() {
                    Text = m,
                    Image = WpfImages.Method16
                  }).Concat(methods.Select(m => (ICompletionData)new AttributeValueCompletionData() {
                    Text = m,
                    Image = WpfImages.MethodFriend16
                  })));
                  break;
                case "access_type":
                  items = AttributeValues("can_add", "can_delete", "can_get", "can_update");
                  break;
                case "doGetItem":
                case "version":
                case "isCriteria":
                case "related_expand":
                case "serverEvents":
                case "removeSysProps":
                  items = Promises.Resolved<IEnumerable<ICompletionData>>(new ICompletionData[] {
                    new AttributeValueCompletionData() {
                      Text = "0",
                      Image = WpfImages.EnumValue16,
                      Content = "0 (False)"
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "1",
                      Image = WpfImages.EnumValue16,
                      Content = "1 (True)"
                    }
                  });
                  break;
                case "id":
                  var newGuid = new AttributeValueCompletionData()
                  {
                    Text = "(New Guid)",
                    Content = FormatText.ColorText("(New Guid)", Brushes.Purple),
                    Action = () => Guid.NewGuid().ToString("N").ToUpperInvariant()
                  };
                  items = Promises.Resolved(Enumerable.Repeat<ICompletionData>(newGuid, 1));
                  break;
                case "queryType":
                  items = AttributeValues("Effective", "Latest", "Released");
                  break;
                case "orderBy":
                  if (!string.IsNullOrEmpty(path.Last().Type)
                    && _metadata.ItemTypeByName(path.Last().Type, out itemType))
                  {
                    var lastComma = value.LastIndexOf(",");
                    if (lastComma >= 0) value = value.Substring(lastComma + 1).Trim();

                    items = new OrderByPropertyFactory(_metadata, itemType).GetPromise();
                  }
                  break;
                case "select":
                  if (!string.IsNullOrEmpty(path.Last().Type)
                    && _metadata.ItemTypeByName(path.Last().Type, out itemType))
                  {
                    string partial;
                    var selectPath = SelectPath(value, out partial);
                    value = partial;

                    var itPromise = new Promise<ItemType>();
                    RecurseProperties(itemType, selectPath, it => itPromise.Resolve(it));

                    items = itPromise
                      .Continue(it => new SelectPropertyFactory(_metadata, it).GetPromise());
                  }
                  break;
                case "type":
                  if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(path.Last().Type))
                  {
                    items = Promises.Resolved(Enumerable.Repeat<ICompletionData>(new AttributeValueCompletionData()
                    {
                      Text = path.Last().Type
                    }, 1));
                  }
                  else if (path.Count > 2
                    && path[path.Count - 3].LocalName == "Item"
                    && path[path.Count - 2].LocalName == "Relationships")
                  {
                    if (!string.IsNullOrEmpty(path[path.Count - 3].Type)
                      && _metadata.ItemTypeByName(path[path.Count - 3].Type, out itemType))
                    {
                      items = Promises.Resolved(ItemTypeCompletion<AttributeValueCompletionData>(itemType.Relationships));
                    }
                  }
                  else
                  {
                    items = Promises.Resolved(ItemTypeCompletion<AttributeValueCompletionData>(_metadata.ItemTypes));
                  }
                  break;
                case "typeId":
                  if (!string.IsNullOrEmpty(path.Last().Type)
                    && _metadata.ItemTypeByName(path.Last().Type, out itemType))
                  {
                    items = Promises.Resolved(Enumerable.Repeat<ICompletionData>(new AttributeValueCompletionData()
                    {
                      Text = itemType.Id
                    }, 1));
                  }
                  else if (path.Count > 2
                    && path[path.Count - 3].LocalName == "Item"
                    && path[path.Count - 2].LocalName == "Relationships")
                  {
                    if (!string.IsNullOrEmpty(path[path.Count - 3].Type)
                      && _metadata.ItemTypeByName(path[path.Count - 3].Type, out itemType))
                    {
                      items = Promises.Resolved(ItemTypeCompletion<AttributeValueCompletionData>(itemType.Relationships, true));
                    }
                  }
                  else
                  {
                    items = Promises.Resolved(ItemTypeCompletion<AttributeValueCompletionData>(_metadata.ItemTypes, true));
                  }
                  break;
                case "where":
                  if (!string.IsNullOrEmpty(path.Last().Type)
                    && _metadata.ItemTypeByName(path.Last().Type, out itemType))
                  {
                    return _sql.Completions("select * from innovator.[" + itemType.Name.Replace(' ', '_')
                      + "] where " + value, xml, caret, xml.GetCharAt(caret - value.Length - 1).ToString(), true);
                  }
                  break;
              }
            }
            else
            {
              switch (attrName)
              {
                case "condition":
                  items = Promises.Resolved((IEnumerable<ICompletionData>)new ICompletionData[] {
                    new AttributeValueCompletionData()
                    {
                      Text = "between",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "eq",
                      Content = "eq (=, Equals)",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "ge",
                      Content = "ge (>=, Greather than or equal to)",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "gt",
                      Content = "gt (>, Greather than)",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "in",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "is not null",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "is null",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "is",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "le",
                      Content = "le (<=, Less than or equal to)",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "like",
                      Description = "Both * and % are wildcards",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "lt",
                      Content = "lt (<, Less than)",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "ne",
                      Content = "ne (<>, !=, Not Equals)",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "not between",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "not in",
                      Image = WpfImages.EnumValue16
                    },
                    new AttributeValueCompletionData()
                    {
                      Text = "not like",
                      Image = WpfImages.EnumValue16
                    }
                  });
                  break;
                case "is_null":
                  items = AttributeValues("0", "1");
                  break;
              }
            }

            filter = value;
            break;
          default:
            if (path.Any() && state == XmlState.Tag)
              appendItems = new ICompletionData[] { new BasicCompletionData() { Text = "/" + path.Last().LocalName + ">" } };


            if (path.Count == 1 && path.First().LocalName == "AML" && state == XmlState.Tag)
            {
              items = Elements("Item");
            }
            else if (path.Count == 1 && path.First().LocalName.Equals("sql", StringComparison.OrdinalIgnoreCase) && soapAction == "ApplySQL")
            {
              return _sql.Completions(value, xml, caret, cdata ? "]]>" : "<");
            }
            else if (path.Last().LocalName == "TestSuite")
            {
              items = Elements("Tests", "Init", "Cleanup");
            }
            else if (path.Last().LocalName == "Tests")
            {
              items = Elements("Test");
            }
            else if (path.Last().LocalName == "Test")
            {
              items = Elements("AssertMatch", "Param", "Item", "sql");
            }
            else if (path.Last().LocalName == "Init" || path.Last().LocalName == "Cleanup")
            {
              items = Elements("Param", "Item", "sql");
            }
            else if (path.Last().LocalName == "AssertMatch")
            {
              items = Elements("Expected", "Remove");
            }
            else if (path.Last().LocalName == "Expected" || path.Last().LocalName == "Param")
            {
              items = Elements("Item");
            }
            else
            {
              var j = path.Count - 1;
              while (path[j].LocalName == "and" || path[j].LocalName == "not" || path[j].LocalName == "or") j--;
              var last = path[j];
              if (state == XmlState.Tag && last.LocalName == "Item")
              {
                switch (last.Action)
                {
                  case "AddHistory":
                    items = Elements("action", "filename", "form_name");
                    break;
                  case "GetItemsForStructureBrowser":
                    items = Elements("Item");
                    break;
                  case "EvaluateActivity":
                    items = Elements("Activity", "ActivityAssignment", "Paths", "DelegateTo"
                      , "Tasks", "Variables", "Authentication", "Comments", "Complete");
                    break;
                  case "instantiateWorkflow":
                    items = Elements("WorkflowMap");
                    break;
                  case "promoteItem":
                    items = Elements("state", "comments");
                    break;
                  case "Run Report":
                    items = Elements("report_name", "AML");
                    break;
                  case "SQL Process":
                    items = Elements("name", "PROCESS", "ARG1", "ARG2", "ARG3", "ARG4"
                      , "ARG5", "ARG6", "ARG7", "ARG8", "ARG9");
                    break;
                  default:
                    // Completions for item properties
                    var buffer = new List<ICompletionData>();

                    buffer.Add(new BasicCompletionData("Relationships") { Image = WpfImages.XmlTag16 } );
                    if (last.Action == "get")
                    {
                      buffer.Add(new BasicCompletionData("and") { Image = WpfImages.Operator16 });
                      buffer.Add(new BasicCompletionData("not") { Image = WpfImages.Operator16 });
                      buffer.Add(new BasicCompletionData("or") { Image = WpfImages.Operator16 });
                    }
                    ItemType itemType;
                    if (!string.IsNullOrEmpty(last.Type)
                      && _metadata.ItemTypeByName(last.Type, out itemType))
                    {
                      items = new PropertyCompletionFactory(_metadata, itemType).GetPromise(buffer);
                    }
                    else
                    {
                      items = Promises.Resolved<IEnumerable<ICompletionData>>(buffer);
                    }
                    break;
                }
              }
              else if (state == XmlState.Tag && last.LocalName == "params" && soapAction == "GetAssignedTasks")
              {
                items = Elements("inBasketViewMode", "workflowTasks", "projectTasks", "actionTasks", "userID");
              }
              else if (state == XmlState.Tag && last.LocalName == "Paths" && path.Count > 1 && path[path.Count - 2].Action == "EvaluateActivity")
              {
                items = Elements("Path");
              }
              else if (state == XmlState.Tag && last.LocalName == "Tasks" && path.Count > 1 && path[path.Count - 2].Action == "EvaluateActivity")
              {
                items = Elements("Task");
              }
              else if (state == XmlState.Tag && last.LocalName == "Variables" && path.Count > 1 && path[path.Count - 2].Action == "EvaluateActivity")
              {
                items = Elements("Variable");
              }
              else if (path.Count > 1)
              {
                var lastItem = path.LastOrDefault(n => n.LocalName == "Item");
                if (lastItem != null)
                {
                  if (path.Last().LocalName == "Relationships")
                  {
                    items = CompletionExtensions.GetPromise<BasicCompletionData>("Item");
                  }
                  else if (path.Last().Condition == "in"
                    || path.Last().LocalName.Equals("sql", StringComparison.OrdinalIgnoreCase))
                  {
                    return _sql.Completions(value, xml, caret, cdata ? "]]>" : "<");
                  }
                  else
                  {
                    ItemType itemType;
                    if (lastItem.Action == "Run Report" && path.Last().LocalName.Equals("AML"))
                    {
                      items = Elements("Item");
                    }
                    else if (!string.IsNullOrEmpty(lastItem.Type)
                      && _metadata.ItemTypeByName(lastItem.Type, out itemType))
                    {
                      items = PropertyValueCompletion(itemType, state, lastItem, path).ToPromise();
                    }
                  }
                }
              }
            }

            break;
        }

      }

      if (items == null)
        return Promises.Resolved(new CompletionContext());

      return items.Convert(i => new CompletionContext() {
        Items = FilterAndSort(i.Concat(appendItems), filter),
        Overlap = (filter ?? "").Length
      });
    }

    private static IPromise<IEnumerable<ICompletionData>> Elements(params string[] values)
    {
      return Promises.Resolved(values.Select(v => (ICompletionData)new BasicCompletionData()
      {
        Text = v,
        Image = WpfImages.XmlTag16
      }));
    }

    private static IPromise<IEnumerable<ICompletionData>> Attributes(Func<string, bool> filter, params string[] values)
    {
      return Promises.Resolved(values.Where(filter).Select(v => (ICompletionData)new AttributeCompletionData()
      {
        Text = v,
        Image = WpfImages.Attribute16
      }));
    }
    private static IPromise<IEnumerable<ICompletionData>> AttributeValues(params string[] values)
    {
      return Promises.Resolved(values.Select(v => (ICompletionData)new AttributeValueCompletionData()
      {
        Text = v,
        Image = WpfImages.EnumValue16
      }));
    }

    private IEnumerable<ICompletionData> ItemTypeCompletion<T>(IEnumerable<ItemType> itemTypes, bool insertId = false) where T : BasicCompletionData, new()
    {
      return itemTypes.Select(i =>
      {
        var result = new T();
        result.Text = i.Name;
        result.Image = WpfImages.Class16;
        if (insertId) result.Action = () => i.Id;
        if (!string.IsNullOrWhiteSpace(i.Label)) result.Description = i.Label;
        return result;
      }).Concat(itemTypes.Where(i => !string.IsNullOrWhiteSpace(i.Label) &&
                                     !string.Equals(i.Name, i.Label, StringComparison.OrdinalIgnoreCase))
          .Select(i =>
          {
            var result = new T();
            result.Image = WpfImages.Class16;
            result.Text = i.Label;
            result.Description = i.Name;
            result.Content = FormatText.MutedText(i.Label);
            if (insertId)
              result.Action = () => i.Id;
            else
              result.Action = () => i.Name;
            return result;
          }));
    }

    private static ICompletionData[] _boolCompletions = new ICompletionData[] {
      new BasicCompletionData() {
        Text = "0",
        Content = "0 (False)"
      },
      new BasicCompletionData()
      {
        Text = "1",
        Content = "1 (True)"
      }
    };

    private async Task<IEnumerable<ICompletionData>> PropertyValueCompletion(ItemType itemType, XmlState state, AmlNode lastItem, IList<AmlNode> path)
    {
      string itemValue;

      if (lastItem.Action == "EvaluateActivity")
      {
        var lastName = path.Last().LocalName;
        if (lastName == "Activity")
        {
          return new ICompletionData[]
          {
            new BasicCompletionData()
            {
              Text = "Select context item...",
              Content = FormatText.ColorText("Select context item...", Brushes.Purple),
              Action = () =>
              {
                var items = EditorWindow.GetItems(_conn, "<Item action='get'", 18).ToArray();
                if (items.Length != 1)
                  return string.Empty;

                var activities = _conn.Apply(@"<Item type='Activity' action='get' select='id'>
                                                  <id condition='in'>(select act.id
                                                from innovator.[Workflow] w
                                                inner join innovator.[Workflow_Process] wp
                                                on wp.id = w.related_id
                                                inner join innovator.[Workflow_Process_Activity] wpa
                                                on wpa.source_id = w.related_id
                                                inner join innovator.[Activity] act
                                                on act.id = wpa.related_id
                                                where w.source_id = @0
                                                and wp.state = 'Active'
                                                and wp.locked_by_id is null
                                                and act.state = 'Active'
                                                and act.is_auto = '0')</id>
                                                </Item>", items[0].Unique).Items().ToArray();
                if (activities.Length != 1)
                  return string.Empty;

                return activities[0].Id() + "<!-- " + activities[0].Property("id").KeyedName().Value + " for " + items[0].KeyedName + "-->";
              }
            }
          };
        }
        else if (lastName == "ActivityAssignment" && lastItem.Values.TryGetValue("Activity", out itemValue))
        {
          var assn = (await _conn.ApplyAsync(@"<Item type='Activity Assignment' action='get' select='id,related_id' related_expand='0'>
                                                <is_disabled>0</is_disabled>
                                                <closed_on condition='is null'></closed_on>
                                                <source_id>@0</source_id>
                                              </Item>", true, false, itemValue).ToTask()).Items().ToArray();
          if (assn.Length == 1)
            return Enumerable.Repeat(new BasicCompletionData()
            {
              Text = assn[0].Id() + "<!-- " + assn[0].RelatedId().KeyedName().Value + " -->",
              Content = assn[0].RelatedId().KeyedName().Value
            }, 1);
          return Enumerable.Empty<ICompletionData>();
        }
        else if (lastName == "Paths" && lastItem.Values.TryGetValue("Activity", out itemValue))
        {
          var act = (await _conn.ApplyAsync(@"<Item type='Activity' action='get' select='can_delegate,can_refuse' id='@0'>
                                              <Relationships>
                                                <Item type='Workflow Process Path' action='get' select='id,name,label,is_default'>
                                                </Item>
                                              </Relationships>
                                            </Item>", true, false, itemValue).ToTask()).Items().ToArray();
          if (act.Length != 1)
            return Enumerable.Empty<ICompletionData>();

          var defaultPath = act[0].Relationships("Workflow Process Path")
            .FirstOrNullItem(i => i.Property("is_default").AsBoolean(false))
            ?? act[0].Relationships("Workflow Process Path").FirstOrNullItem();

          var paths = new List<ICompletionData>();
          if (act[0].Property("can_delegate").AsBoolean(true))
          {
            paths.Add(new BasicCompletionData()
            {
              Text = "<Path id='" + defaultPath.Id() + "'>Delegate</Path>",
              Content = "Delegate"
            });
          }
          if (act[0].Property("can_refuse").AsBoolean(true))
          {
            paths.Add(new BasicCompletionData()
            {
              Text = "<Path id='" + defaultPath.Id() + "'>Refuse</Path>",
              Content = "Refuse"
            });
          }
          paths.AddRange(act[0].Relationships("Workflow Process Path").Select(p => new BasicCompletionData()
          {
            Text = "<Path id='" + p.Id() + "'>" + p.Property("label").AsString(p.Property("name").Value) + "</Path>",
            Content = p.Property("label").AsString(p.Property("name").Value)
          }));
          return paths;
        }
        else if (lastName == "Complete")
        {
          return _boolCompletions;
        }
        else
        {
          return Enumerable.Empty<ICompletionData>();
        }
      }
      else
      {
        var p = await _metadata.GetProperty(itemType, path.Last().LocalName).ToTask();

        if (p.Type == PropertyType.item && p.Restrictions.Any())
        {
          var completions = p.Restrictions
            .Select(type => (state != XmlState.Tag ? "<" : "") + "Item type='" + type + "'")
            .GetCompletions<BasicCompletionData>();

          if (p.Restrictions.Any(r => string.Equals(r, "File", StringComparison.OrdinalIgnoreCase)))
          {
            var uploadComplete = new BasicCompletionData()
            {
              Text = "Select file to upload...",
              Content = FormatText.ColorText("Select file to upload...", Brushes.Purple),
              Action = () =>
              {
                using (var dialog = new System.Windows.Forms.OpenFileDialog())
                {
                  dialog.Multiselect = false;
                  if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                  {
                    var upload = _conn.CreateUploadCommand();
                    var query = upload.AddFile(Guid.NewGuid().ToString("N").ToUpperInvariant(),
                      dialog.FileName);
                    if (state == XmlState.Tag)
                      query = query.TrimStart('<');
                    return query;
                  }
                }
                return string.Empty;
              }
            };
            completions = completions.Concat(Enumerable.Repeat(uploadComplete, 1));
          }
          else
          {
            completions = completions.Concat(Enumerable.Repeat(new ItemPropertyCompletionData(_conn, path, p), 1));
          }

          if (p.Restrictions.Any(r => string.Equals(r, "ItemType", StringComparison.OrdinalIgnoreCase)))
          {
            completions = completions.Concat(ItemTypeCompletion<BasicCompletionData>(_metadata.ItemTypes, true));
          }

          return completions;
        }
        else if (p.Type == PropertyType.date)
        {
          return new ICompletionData[] {
          new BasicCompletionData() {
            Text = DateTime.Now.ToString("s"),
            Content = DateTime.Now.ToString("s") + " (Now)"
          }
          // Add a completion dialog option to open a calendar control
        };
        }
        else if (p.Type == PropertyType.boolean)
        {
          return _boolCompletions;
        }
        else if (p.Type == PropertyType.list && !string.IsNullOrEmpty(p.DataSource))
        {
          var values = await _metadata.ListValues(p.DataSource).ToTask();

          var hash = new HashSet<string>(values.Select(v => v.Value), StringComparer.CurrentCultureIgnoreCase);
          return values
            .Select(v => v.Value)
            .GetCompletions<BasicCompletionData>()
            .Concat(values.Where(v => !string.IsNullOrWhiteSpace(v.Label) && !hash.Contains(v.Label))
                    .Select(v => new BasicCompletionData()
                    {
                      Text = v.Label + " (" + v.Value + ")",
                      Description = v.Value,
                      Content = FormatText.MutedText(v.Label + " (" + v.Value + ")"),
                      Action = () => v.Value
                    }));
        }
        else if (p.Name == "classification")
        {
          var paths = await _metadata.GetClassPaths(itemType).ToTask();
          return paths.GetCompletions<BasicCompletionData>();
        }
        else if (p.Name == "name" && itemType.Name == "Property"
          && lastItem.Values.TryGetValue("label", out itemValue)
          && (lastItem.Action == "add" || lastItem.Action == "create"
            || lastItem.Action == "edit" || lastItem.Action == "merge"))
        {
          var output = new char[itemValue.Length];
          var o = 0;
          for (var i = 0; i < itemValue.Length; i++)
          {
            if (char.IsLetterOrDigit(itemValue[i]))
            {
              output[o] = char.ToLowerInvariant(itemValue[i]);
              o++;
            }
            else if (o == 0 || output[o-1] != '_')
            {
              output[o] = '_';
              o++;
            }
          }
          return Enumerable.Repeat(new string(output, 0, o), 1).GetCompletions<BasicCompletionData>();
        }
        else if (p.Name == "name" && itemType.Name == "Property" && lastItem.Action == "get"
          && lastItem.Values.TryGetValue("source_id", out itemValue))
        {
          var parentType = _metadata.TypeById(itemValue);
          return await new PropertyCompletionFactory(_metadata, parentType).GetPromise().ToTask();
        }
        else if (p.Name == "class_path" && itemType.Name == "Property"
          && lastItem.Values.TryGetValue("source_id", out itemValue))
        {
          var parentType = _metadata.TypeById(itemValue);
          var paths = await _metadata.GetClassPaths(parentType).ToTask();
          return paths.GetCompletions<BasicCompletionData>();
        }
        else if (p.Name == "name" && itemType.Name == "Method" && lastItem.Action == "get")
        {
          return _metadata.MethodNames.GetCompletions<BasicCompletionData>();
        }
        else if (p.Name == "name" && itemType.Name == "ItemType" && lastItem.Action == "get")
        {
          return ItemTypeCompletion<BasicCompletionData>(_metadata.ItemTypes);
        }
        else if (p.Name == "state" && lastItem.Action == "promoteItem"
          && !string.IsNullOrEmpty(lastItem.Type) && !string.IsNullOrEmpty(lastItem.Id))
        {
          var stateResult = await _conn.ApplyAsync(@"<Item type='@0' action='getItemNextStates' id='@1'></Item>"
            , true, false, lastItem.Type, lastItem.Id).ToTask();
          var states = stateResult.Items().Select(i => i.Property("to_state").AsItem().Property("name").Value).ToArray();
          return states.GetCompletions<BasicCompletionData>();
        }
        else if (p.Name == "state" && !string.IsNullOrEmpty(lastItem.Type))
        {
          var states = await _metadata.ItemTypeStates(itemType).ToTask();
          return states.GetCompletions<BasicCompletionData>();
        }
        else
        {
          return Enumerable.Empty<ICompletionData>();
        }
      }
    }

    private class ItemPropertyCompletionData : ICompletionData
    {
      private IAsyncConnection _conn;
      private IList<AmlNode> _path;
      private Property _prop;

      public ItemPropertyCompletionData(IAsyncConnection conn, IList<AmlNode> path, Property prop)
      {
        _conn = conn;
        _path = path;
        _prop = prop;
      }

      public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, EventArgs insertionRequestEventArgs)
      {
        var item = _path.LastOrDefault(n => n.LocalName == "Item");
        if (item == null)
          return;

        var query = string.Format("<Item type='{0}' action='get'></Item>", _prop.Restrictions.First());
        var items = EditorWindow.GetItems(_conn, query, query.Length - 7);
        if (items.Any(i => _prop.Restrictions.Contains(i.Type)))
        {
          var allItems = items.Where(i => _prop.Restrictions.Contains(i.Type)).ToArray();
          if (item.Action == "add" || item.Action == "create")
          {
            if (allItems.Length == 1)
            {
              PerformComplete(textArea, completionSegment, allItems);
            }
            else
            {
              var start = item.Offset;
              var doc = textArea.Document;
              while (start > 0 && (doc.GetCharAt(start - 1) == '\t' || doc.GetCharAt(start - 1) == ' '))
                start--;
              var begin = start;
              var end = completionSegment.Offset;
              while (doc.GetCharAt(end) != '>')
                end--;
              var prefix = doc.GetText(start, end - start);
              start = completionSegment.EndOffset;
              end = doc.IndexOf("</Item>", completionSegment.EndOffset, doc.TextLength - completionSegment.EndOffset, StringComparison.Ordinal);
              end = end < 0 ? doc.TextLength : end + 7;
              var suffix = doc.GetText(start, end - start);
              var text = allItems
                .Select(i => prefix + " type='" + i.Type + "' keyed_name='" + i.KeyedName + "'>" + i.Unique + suffix)
                .GroupConcat(Environment.NewLine);
              doc.Replace(begin, end - begin, text);
            }
          }
          else if (item.Action == "merge" || item.Action == "edit" || item.Action == "update")
          {
            if (allItems.Length > 1)
              throw new Exception("Can only set a property value to a single item");
            PerformComplete(textArea, completionSegment, allItems);
          }
          else
          {
            PerformComplete(textArea, completionSegment, allItems);
          }
        }
      }

      private void PerformComplete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, ItemReference[] allItems)
      {
        var start = completionSegment.Offset;
        var end = completionSegment.EndOffset;
        var doc = textArea.Document;
        while (doc.GetCharAt(start) != '>')
          start--;

        if (allItems.Length == 1)
        {
          doc.Replace(start, end - start, " type='" + allItems[0].Type + "' keyed_name='" + allItems[0].KeyedName + "'>" + allItems[0].Unique);
        }
        else
        {
          doc.Replace(start, end - start, " condition='in'>'" + allItems.GroupConcat("','", i => i.Unique) + "'");
        }
      }

      public object Content
      {
        get { return FormatText.ColorText(this.Text, Brushes.Purple); }
      }

      public object Description
      {
        get { return null; }
      }

      public ImageSource Image
      {
        get { return null; }
      }

      public double Priority
      {
        get { return 0; }
      }

      public string Text
      {
        get { return "Search for item(s).."; }
      }
    }

    private void RecurseProperties(ItemType itemType, IEnumerable<string> remainingPath, Action<ItemType> callback)
    {
      if (remainingPath.Any())
      {
        _metadata.GetProperty(itemType, remainingPath.First())
        .Done(currProp =>
        {
          ItemType it;
          if (currProp.Type == PropertyType.item
            && currProp.Restrictions.Any()
            && _metadata.ItemTypeByName(currProp.Restrictions.First(), out it))
          {
            RecurseProperties(it, remainingPath.Skip(1), callback);
          }
          else
          {
            callback(itemType);
          }
        })
        .Fail(ex => callback(itemType));
      }
      else
      {
        callback(itemType);
      }
    }

    public virtual void InitializeConnection(IAsyncConnection conn)
    {
      _conn = conn;
      _metadata = ArasMetadataProvider.Cached(conn);
      _isInitialized = true;
    }

    public string LastOpenTag(ITextSource xml)
    {
      bool isOpenTag = false;
      var path = new List<AmlNode>();

      var state = XmlUtils.ProcessFragment(xml, (r, o, st) =>
      {
        switch (r.NodeType)
        {
          case XmlNodeType.Element:
            if (!r.IsEmptyElement)
            {
              path.Add(new AmlNode() {
                LocalName = r.LocalName
              });
              isOpenTag = true;
            }
            break;
          case XmlNodeType.EndElement:
            path.RemoveAt(path.Count - 1);
            isOpenTag = false;
            break;
          case XmlNodeType.Attribute:
            // Do nothing
            break;
          default:
            isOpenTag = false;
            break;
        }
        return true;
      });

      if (isOpenTag && path.Any()) return path.Last().LocalName;
      return null;
    }

    private IEnumerable<ICompletionData> FilterAndSort(IEnumerable<ICompletionData> values, string substring)
    {
      return values
        .Where(i => string.IsNullOrEmpty(substring)
          || i.Text.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
        .OrderBy(i => (string.IsNullOrEmpty(substring)
          || i.Text.StartsWith(substring, StringComparison.CurrentCultureIgnoreCase)) ? 0 : 1)
        .ThenBy(i => i.Text.StartsWith("/") ? 1 : 0)
        .ThenBy(i => i.Text)
        .ToArray();
    }


    private List<string> SelectPath(string selectStr, out string partial)
    {
      partial = null;
      var lastOperator = -1;
      var path = new List<string>();

      for (var i = 0; i < selectStr.Length; i++)
      {
        if (selectStr[i] == '(' || selectStr[i] == ')' || selectStr[i] == ',')
        {
          switch (selectStr[i])
          {
            case '(':
              path.Add(selectStr.Substring(lastOperator + 1, i - lastOperator - 1).Trim());
              break;
            case ')':
              path.RemoveAt(path.Count - 1);
              break;
          }
          lastOperator = i;
        }
      }
      if (lastOperator < selectStr.Length - 1)
      {
        partial = selectStr.Substring(lastOperator + 1).Trim();
      }
      return path;
    }

    private class AmlNode
    {
      private Dictionary<string, string> _values = new Dictionary<string,string>();

      public int Offset { get; set; }
      public string LocalName { get; set; }
      public string Type { get; set; }
      public string Id { get; set; }
      public string Action { get; set; }
      public string Condition { get; set; }
      public Dictionary<string, string> Values { get { return _values; } }
    }

    public IEnumerable<string> GetSchemaNames()
    {
      return Enumerable.Repeat("innovator", 1);
    }

    public IEnumerable<string> GetTableNames()
    {
      return _metadata.ItemTypes
        .Select(i => "innovator.[" + i.Name.Replace(' ', '_') + "]")
        .Concat(_metadata.Sqls()
          .Where(s => string.Equals(s.Type, "type", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Type, "view", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Type, "function", StringComparison.OrdinalIgnoreCase))
          .Select(s => "innovator.[" + s.KeyedName + "]"));
    }

    public IPromise<IEnumerable<IListValue>> GetColumnNames(string tableName)
    {
      ItemType itemType;
      if (tableName.StartsWith("innovator.", StringComparison.OrdinalIgnoreCase))
        tableName = tableName.Substring(10);

      if (_metadata.ItemTypeByName(tableName.Replace('_', ' '), out itemType))
        return _metadata.GetProperties(itemType).Convert(p => p.OfType<IListValue>());

      return Promises.Resolved(Enumerable.Empty<IListValue>());
    }


    private class SelectPropertyFactory : PropertyCompletionFactory
    {
      public SelectPropertyFactory(ArasMetadataProvider metadata, ItemType itemType) :
        base(metadata, itemType) { }

      protected override BasicCompletionData CreateCompletion()
      {
        return new AttributeValueCompletionData() { MultiValue = true };
      }
    }

    private class OrderByPropertyFactory : PropertyCompletionFactory
    {
      public OrderByPropertyFactory(ArasMetadataProvider metadata, ItemType itemType) :
        base(metadata, itemType) { }

      protected override BasicCompletionData CreateCompletion()
      {
        return new AttributeValueCompletionData() { MultiValue = true };
      }

      protected override IEnumerable<ICompletionData> GetCompletions(IEnumerable<IListValue> normal, IEnumerable<IListValue> byLabel)
      {
        return base.GetCompletions(normal, byLabel)
          .Concat(normal.Select(i =>
          {
            var data = ConfigureNormalProperty(CreateCompletion(), i);
            data.Text += " DESC";
            data.Description += " DESC";
            return data;
          }))
          .Concat(byLabel.Select(i =>
          {
            var data = ConfigureLabeledProperty(CreateCompletion(), i);
            data.Text += " DESC";
            data.Description += " DESC";
            data.Content = FormatText.MutedText(data.Text);
            data.Action = () => i.Value + " DESC";
            return data;
          }));
      }
    }

    private class WherePropertyFactory : PropertyCompletionFactory
    {
      public WherePropertyFactory(ArasMetadataProvider metadata, ItemType itemType) :
        base(metadata, itemType) { }

      protected override BasicCompletionData CreateCompletion()
      {
        return new AttributeValueCompletionData() { MultiValue = true };
      }
      protected override BasicCompletionData ConfigureNormalProperty(BasicCompletionData data, IListValue prop)
      {
        var res = base.ConfigureNormalProperty(data, prop);
        res.Action = () => "[" + _itemType.Name.Replace(' ', '_') + "].[" + prop.Value + "]";
        return res;
      }
    }

    public IEnumerable<string> GetFunctionNames(bool tableValued)
    {
      return Enumerable.Empty<string>();
    }
  }
}
