using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace ButlerdSharp.Generator
{
    [Generator]
    public class MessageGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public Spec ReadSpec()
        {
            using var httpClient = new HttpClient();
            return JsonConvert.DeserializeObject<Spec>(httpClient.GetStringAsync("https://github.com/js6pak/butler/releases/download/v15.21.0/butlerd.json").GetAwaiter().GetResult());
        }

        private readonly Regex _mapRegex = new Regex(@"\{ \[key\: (?<key>\w+)\]\: (?<value>\w+) \}");

        private string TransformType(string type)
        {
            var match = _mapRegex.Match(type);
            if (match.Success)
            {
                return $"Dictionary<{match.Groups["key"]}, {match.Groups["value"]}>";
            }

            return type
                .Replace("number", "int")
                .Replace("boolean", "bool")
                .Replace("Actions", "Action[]")
                .Replace("Cursor", "bool")
                .Replace("RFCDate", "string");
        }

        public string MakeNullable(string name, bool optional)
        {
            var value = TransformType(name);
            if (optional)
                value += "?";

            return value;
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                var spec = ReadSpec();

                const string baseNamespace = "ButlerdSharp.Protocol";

                foreach (var request in spec.Requests)
                {
                    ClassInfo current = null;

                    var split = request.Method.Split('.').Reverse().Append("Requests").ToArray();
                    foreach (var s in split)
                    {
                        string FixName(string name)
                        {
                            return spec.Requests.Any(x => x.Method.Contains(name.Replace("[]", ""))) ? $"Structs.{name}" : name;
                        }

                        if (current == null)
                        {
                            ClassInfo requestInfo;

                            current = new ClassInfo
                            {
                                Namespace = baseNamespace + ".Requests",
                                Doc = request.Doc,
                                Name = s,
                                IsPartial = true,
                                IsStatic = true,
                                Content =
                                {
                                    new FieldInfo
                                    {
                                        Name = "Id",
                                        IsConst = true,
                                        Type = "string",
                                        Value = $"\"{request.Method}\""
                                    },

                                    (requestInfo = new ClassInfo
                                    {
                                        Name = "Request",
                                        Content =
                                        {
                                            new MethodInfo
                                            {
                                                ReturnType = "Task<Response>",
                                                Name = "SendAsync",
                                                Parameters =
                                                {
                                                    new ParameterInfo
                                                    {
                                                        Name = "jsonRpc",
                                                        Type = "JsonRpc"
                                                    }
                                                },
                                                Body = writer => writer.WriteLine($"return jsonRpc.InvokeWithParameterObjectAsync<Response>({s}.Id, this);")
                                            }
                                        }
                                    }),

                                    new ClassInfo
                                    {
                                        Name = "Response",
                                        Content = request.Result.Fields?.Select(field => new PropertyInfo
                                        {
                                            Doc = field.Doc,
                                            Name = field.Name,
                                            Type = MakeNullable(FixName(field.Type), field.Optional)
                                        }).Cast<IClassInfoContent>().ToList()
                                    }.AddJsonConstructor()
                                }
                            };

                            if (request.Params.Fields != null)
                            {
                                requestInfo.Content.AddRange(request.Params.Fields.Select(field => new PropertyInfo
                                {
                                    Doc = field.Doc,
                                    Name = field.Name,
                                    Type = MakeNullable(FixName(field.Type), field.Optional)
                                }));
                            }

                            requestInfo.AddJsonConstructor();
                        }
                        else
                        {
                            var classInfo = new ClassInfo
                            {
                                Namespace = baseNamespace + ".Requests",
                                Name = s,
                                IsPartial = true,
                                IsStatic = true
                            };

                            classInfo.Content.Add(current);
                            current = classInfo;
                        }
                    }

                    var classWriter = new ClassWriter();
                    classWriter.WriteFile(current);

                    context.AddSource(request.Method, classWriter.ToString());
                }

                foreach (var notification in spec.Notifications)
                {
                    ClassInfo current = null;

                    foreach (var s in notification.Method.Split('.').Reverse())
                    {
                        if (current == null)
                        {
                            current = new ClassInfo
                            {
                                Namespace = baseNamespace + ".Notifications",
                                Doc = notification.Doc,
                                Name = s,
                                Content =
                                {
                                    new FieldInfo
                                    {
                                        Name = "Id",
                                        IsConst = true,
                                        Type = "string",
                                        Value = $"\"{notification.Method}\""
                                    },
                                }
                            };

                            if (notification.Params.Fields != null)
                            {
                                current.Content.AddRange(notification.Params.Fields.Select(field => new PropertyInfo
                                {
                                    Doc = field.Doc,
                                    Name = field.Name,
                                    Type = MakeNullable(field.Type, field.Optional)
                                }));
                            }

                            current.AddJsonConstructor();
                        }
                        else
                        {
                            var classInfo = new ClassInfo
                            {
                                Namespace = baseNamespace + ".Notifications",
                                Doc = notification.Doc,
                                Name = s,
                                IsPartial = true
                            };

                            classInfo.Content.Add(current);
                            current = classInfo;
                        }
                    }

                    var classWriter = new ClassWriter();
                    classWriter.WriteFile(current);

                    context.AddSource(notification.Method, classWriter.ToString());
                }

                foreach (var structType in spec.StructTypes)
                {
                    var classWriter = new ClassWriter();
                    classWriter.WriteFile(new ClassInfo
                    {
                        Doc = structType.Doc,
                        Namespace = baseNamespace + ".Structs",
                        Name = structType.Name,
                        Content = structType.Fields?.Select(field => new PropertyInfo
                        {
                            Doc = field.Doc,
                            Name = field.Name,
                            Type = MakeNullable(field.Type, field.Optional)
                        }).Cast<IClassInfoContent>().ToList()
                    }.AddJsonConstructor());

                    context.AddSource(structType.Name, classWriter.ToString());
                }

                foreach (var enumType in spec.EnumTypes)
                {
                    var enumInfo = new EnumInfo
                    {
                        Doc = enumType.Doc,
                        Namespace = baseNamespace + ".Enums",
                        Name = enumType.Name,
                        Content = enumType.Values?.Select(value => new EnumValueInfo
                        {
                            Doc = value.Doc,
                            Name = value.Name,
                            Value = value.Value
                        }).Cast<IEnumInfoContent>().ToList()
                    };

                    // TODO ask upstream wtf is going on here?
                    if (enumType.Name == "BuildState")
                    {
                        enumInfo.Content!.Insert(0, new EnumValueInfo
                        {
                            Doc = "Unknown build state, presumably because you don't have permissions to see it",
                            Name = "Unknown",
                            Value = "\"\""
                        });
                    }

                    var classWriter = new ClassWriter();
                    classWriter.WriteFile(enumInfo);

                    context.AddSource(enumType.Name, classWriter.ToString());
                }
            }
            catch (Exception e)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "ERROR",
                        "An exception was thrown by the MessageGenerator",
                        $"An exception was thrown by the MessageGenerator generator: {e.ToString().Replace("\n", ",")}",
                        "MessageGenerator",
                        DiagnosticSeverity.Error,
                        true),
                    Location.None));
            }
        }
    }
}
