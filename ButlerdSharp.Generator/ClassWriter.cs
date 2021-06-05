using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ButlerdSharp.Generator
{
    public class ClassWriter
    {
        public StringBuilder SourceBuilder { get; }

        public int IndentLevel { get; set; }

        private string Indent => new string('\t', IndentLevel);

        public ClassWriter(StringBuilder sourceBuilder)
        {
            SourceBuilder = sourceBuilder;
        }

        public ClassWriter() : this(new StringBuilder())
        {
        }

        public void Write(string text)
        {
            SourceBuilder.Append(Indent + text);
        }

        public void WriteLine(string text)
        {
            SourceBuilder.AppendLine(Indent + text);
        }

        public void WriteLine()
        {
            SourceBuilder.AppendLine();
        }

        public void WriteUsing(string @namespace)
        {
            WriteLine($"using {@namespace};");
        }

        public IDisposable StartNamespace(string @namespace)
        {
            WriteLine($"namespace {@namespace}");
            return StartBlock();
        }

        public IDisposable StartBlock()
        {
            WriteLine("{");
            IndentLevel++;

            return new CallbackDisposable(() =>
            {
                IndentLevel--;
                WriteLine("}");
            });
        }

        public void WriteDoc(string doc)
        {
            if (!string.IsNullOrWhiteSpace(doc))
            {
                WriteLine("/// <summary>");

                foreach (var s in doc.Split('\n'))
                {
                    WriteLine($"/// {s}");
                }

                WriteLine("/// </summary>");
            }
        }

        public static string ToPascalCase(string text)
        {
            const string pattern = @"(-|_)\w{1}|^\w";
            var result = Regex.Replace(text, pattern, match => match.Value.Replace("-", string.Empty).Replace("_", string.Empty).ToUpper());

            if (int.TryParse(result[0].ToString(), out _))
            {
                result = "_" + result;
            }

            return result;
        }

        private void Write<T>(IEnumerable<T> enumerable, Action<T> write, string separator = null)
        {
            if (enumerable == null)
                return;

            var array = enumerable as T[] ?? enumerable.ToArray();

            var i = 0;
            foreach (var x in array)
            {
                i++;

                write(x);

                if (i < array.Length)
                {
                    if (separator == null)
                    {
                        WriteLine();
                    }
                    else
                    {
                        SourceBuilder.Append(separator);
                    }
                }
            }
        }

        public void WriteType(TypeInfo typeInfo)
        {
            WriteDoc(typeInfo.Doc);

            if (typeInfo is EnumInfo beforeEnumInfo && beforeEnumInfo.Content.OfType<EnumValueInfo>().Any(x => x.Value.Contains("\"")))
            {
                WriteLine(@"[JsonConverter(typeof(StringEnumConverter))]");
            }

            WriteLine($"public{(typeInfo.IsStatic ? " static" : "")}{(typeInfo.IsPartial ? " partial" : "")} {typeInfo.Type} {typeInfo.Name}");
            using (StartBlock())
            {
                switch (typeInfo)
                {
                    case ClassInfo classInfo:
                    {
                        Write(classInfo.Content, content =>
                        {
                            switch (content)
                            {
                                case ClassInfo nested:
                                {
                                    WriteType(nested);
                                    break;
                                }

                                case FieldInfo fieldInfo:
                                {
                                    WriteDoc(fieldInfo.Doc);
                                    WriteLine($"public const {fieldInfo.Type} {fieldInfo.Name} = {fieldInfo.Value};");

                                    break;
                                }

                                case PropertyInfo propertyInfo:
                                {
                                    WriteDoc(propertyInfo.Doc);

                                    var fixedName = propertyInfo.GetFixedName(typeInfo);

                                    if (fixedName != propertyInfo.Name)
                                    {
                                        WriteLine($"[JsonProperty(\"{propertyInfo.Name}\")]");
                                    }

                                    WriteLine($"public {propertyInfo.Type} {fixedName} {{ get; set; }}");

                                    break;
                                }

                                case MethodInfo methodInfo:
                                {
                                    foreach (var attribute in methodInfo.Attributes)
                                    {
                                        WriteLine(attribute);
                                    }

                                    Write(methodInfo is ConstructorInfo
                                        ? $"public {typeInfo.Name}("
                                        : $"public {methodInfo.ReturnType} {methodInfo.Name}("
                                    );

                                    Write(methodInfo.Parameters, parameterInfo =>
                                    {
                                        SourceBuilder.Append($"{parameterInfo.Type} {parameterInfo.Name}");

                                        if (parameterInfo.Type.EndsWith("?"))
                                        {
                                            SourceBuilder.Append(" = null");
                                        }
                                    }, ", ");

                                    SourceBuilder.AppendLine(")");

                                    using (StartBlock())
                                    {
                                        methodInfo.Body?.Invoke(this);
                                    }

                                    break;
                                }

                                default:
                                    throw new ArgumentOutOfRangeException(nameof(content));
                            }
                        });

                        break;
                    }
                    case EnumInfo enumInfo:
                    {
                        Write(enumInfo.Content, content =>
                        {
                            switch (content)
                            {
                                case ClassInfo nested:
                                {
                                    WriteType(nested);
                                    break;
                                }

                                case EnumValueInfo enumValueInfo:
                                {
                                    WriteDoc(enumValueInfo.Doc);

                                    if (enumValueInfo.Value.Contains("\""))
                                    {
                                        WriteLine($"[EnumMember(Value = {enumValueInfo.Value})]");
                                        WriteLine($"{ToPascalCase(enumValueInfo.Name)},");
                                    }
                                    else
                                    {
                                        WriteLine($"{ToPascalCase(enumValueInfo.Name)} = {enumValueInfo.Value},");
                                    }

                                    break;
                                }

                                default:
                                    throw new ArgumentOutOfRangeException(nameof(content));
                            }
                        });

                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException(nameof(typeInfo));
                }
            }
        }

        public void WriteFile(TypeInfo typeInfo)
        {
            WriteUsing("System.Runtime.Serialization");
            WriteUsing("System.Collections.Generic");
            WriteUsing("System.Threading.Tasks");
            WriteUsing("Newtonsoft.Json");
            WriteUsing("Newtonsoft.Json.Converters");
            WriteUsing("StreamJsonRpc");
            WriteUsing("ButlerdSharp.Protocol.Structs");
            WriteUsing("ButlerdSharp.Protocol.Enums");

            WriteLine();

            using (StartNamespace(typeInfo.Namespace))
            {
                WriteType(typeInfo);
            }
        }

        public override string ToString()
        {
            return SourceBuilder.ToString();
        }
    }

    public class CallbackDisposable : IDisposable
    {
        private readonly Action _callback;

        public CallbackDisposable(Action callback)
        {
            _callback = callback;
        }

        public void Dispose()
        {
            _callback.Invoke();
        }
    }

    public interface IClassInfoContent
    {
    }

    public interface IEnumInfoContent
    {
    }

    public abstract class TypeInfo : IClassInfoContent, IEnumInfoContent
    {
        public abstract string Type { get; }

        public string Doc { get; init; }

        public string Namespace { get; init; }
        public string Name { get; init; }

        public bool IsPartial { get; init; }
        public bool IsStatic { get; init; }
    }

    public class ClassInfo : TypeInfo
    {
        public override string Type => "class";

        public List<IClassInfoContent> Content { get; init; } = new List<IClassInfoContent>();

        private string Escape(string name)
        {
            return name switch
            {
                "continue" => "@" + name,
                _ => name
            };
        }

        public ClassInfo AddJsonConstructor()
        {
            var properties = Content?.OfType<PropertyInfo>().ToArray();

            if (properties != null && properties.Any())
            {
                Content.Insert(0, new ConstructorInfo
                {
                    Attributes =
                    {
                        "[JsonConstructor]"
                    },
                    Parameters = properties.OrderBy(x => x.Type.EndsWith("?")).Select(field => new ParameterInfo
                    {
                        Name = Escape(field.Name),
                        Type = field.Type
                    }).ToList(),
                    Body = writer =>
                    {
                        foreach (var property in properties)
                        {
                            writer.WriteLine(@$"this.{property.GetFixedName(this)} = {Escape(property.Name)};");
                        }
                    }
                });
            }

            return this;
        }
    }

    public class MethodInfo : IClassInfoContent
    {
        public string Doc { get; init; }

        public List<string> Attributes { get; init; } = new List<string>();

        public string ReturnType { get; init; }

        public string Name { get; init; }

        public List<ParameterInfo> Parameters { get; init; } = new List<ParameterInfo>();

        public Action<ClassWriter> Body { get; init; }
    }

    public class ConstructorInfo : MethodInfo
    {
    }

    public class ParameterInfo
    {
        public string Type { get; init; }

        public string Name { get; init; }
    }

    public class PropertyInfo : IClassInfoContent
    {
        public string Doc { get; init; }

        public string Type { get; init; }

        public string Name { get; init; }

        public string GetFixedName(TypeInfo typeInfo)
        {
            var name = ClassWriter.ToPascalCase(Name);
            if (name == typeInfo.Name)
            {
                name += "Value";
            }

            return name;
        }
    }

    public class FieldInfo : IClassInfoContent
    {
        public string Doc { get; init; }

        public string Type { get; init; }

        public string Name { get; init; }

        public string Value { get; init; }

        public bool IsConst { get; init; }
    }

    public class EnumInfo : TypeInfo
    {
        public override string Type => "enum";

        public List<IEnumInfoContent> Content { get; init; } = new List<IEnumInfoContent>();
    }

    public class EnumValueInfo : IEnumInfoContent
    {
        public string Doc { get; init; }

        public string Name { get; init; }

        public string Value { get; init; }
    }
}
