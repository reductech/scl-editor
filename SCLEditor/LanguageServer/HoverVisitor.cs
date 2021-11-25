﻿using CSharpFunctionalExtensions;
using Namotion.Reflection;
using Reductech.EDR.Core.Internal.Serialization;
using Entity = CSharpFunctionalExtensions.Entity;

namespace Reductech.Utilities.SCLEditor.LanguageServer;

/// <summary>
/// Visits SCL to find hover
/// </summary>
public class HoverVisitor : SCLBaseVisitor<QuickInfoResponse?>
{
    /// <summary>
    /// Create a new HoverVisitor
    /// </summary>
    public HoverVisitor(
        LinePosition position,
        LinePosition positionOffset,
        StepFactoryStore stepFactoryStore,
        Lazy<Result<TypeResolver, IError>> lazyTypeResolver)
    {
        LinePosition       = position;
        LinePositionOffset = positionOffset;
        StepFactoryStore   = stepFactoryStore;
        LazyTypeResolver   = lazyTypeResolver;
    }

    /// <summary>
    /// Creates a type resolver lazily
    /// </summary>
    public static Lazy<Result<TypeResolver, IError>> CreateLazyTypeResolver(
        string fullSCL,
        StepFactoryStore stepFactoryStore)
    {
        var resolver = new Lazy<Result<TypeResolver, IError>>(
            () =>
                SCLParsing.TryParseStep(fullSCL)
                    .Bind(
                        x => TypeResolver.TryCreate(
                            stepFactoryStore,
                            SCLRunner.RootCallerMetadata,
                            Maybe<VariableName>.None,
                            x
                        )
                    )
        );

        return resolver;
    }

    /// <summary>
    /// The position of the hover
    /// </summary>
    public LinePosition LinePosition { get; }

    /// <summary>
    /// The position offset
    /// </summary>
    public LinePosition LinePositionOffset { get; }

    /// <summary>
    /// The Step Factory Store
    /// </summary>
    public StepFactoryStore StepFactoryStore { get; }

    /// <summary>
    /// A Lazy Type Resolver
    /// </summary>
    public Lazy<Result<TypeResolver, IError>> LazyTypeResolver { get; }

    /// <inheritdoc />
    protected override bool ShouldVisitNextChild(IRuleNode node, QuickInfoResponse? currentResult)
    {
        return currentResult == null;
    }

    /// <inheritdoc />
    public override QuickInfoResponse? Visit(IParseTree tree)
    {
        if (tree is ParserRuleContext context && context.ContainsPosition(LinePosition))
            return base.Visit(tree);

        return DefaultResult;
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitFunction(SCLParser.FunctionContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        var name = context.NAME().GetText();

        if (StepFactoryStore.Dictionary.TryGetValue(name, out var stepFactory))
        {
            if (!context.NAME().Symbol.ContainsPosition(LinePosition))
            {
                var positionalTerms = context.term();

                for (var index = 0; index < positionalTerms.Length; index++)
                {
                    var term = positionalTerms[index];

                    if (term.ContainsPosition(LinePosition))
                    {
                        int trueIndex;

                        if (context.Parent is SCLParser.PipeFunctionContext pipeFunctionContext &&
                            pipeFunctionContext.children.Last() == context)
                        {
                            trueIndex = index + 2;
                        }
                        else
                        {
                            trueIndex = index + 1;
                        }

                        var indexReference = new StepParameterReference.Index(trueIndex);

                        if (
                            stepFactory.ParameterDictionary.TryGetValue(
                                indexReference,
                                out var stepParameter
                            ))
                        {
                            var nHover = Visit(term);

                            if (nHover is null)
                            {
                                return Description(
                                    stepParameter.Name,
                                    stepParameter.ActualType.Name,
                                    stepParameter.Summary
                                );
                            }

                            return nHover;
                        }

                        return Error($"Step '{name}' does not take an argument {index}");
                    }
                }

                foreach (var namedArgumentContext in context.namedArgument())
                {
                    if (namedArgumentContext.ContainsPosition(LinePosition))
                    {
                        var argumentName = namedArgumentContext.NAME().GetText();

                        if (stepFactory.ParameterDictionary.TryGetValue(
                                new StepParameterReference.Named(argumentName),
                                out var stepParameter
                            ))
                        {
                            var nHover = Visit(namedArgumentContext);

                            if (nHover is null)
                                return Description(
                                    stepParameter.Name,
                                    stepParameter.ActualType.Name,
                                    stepParameter.Summary
                                );

                            return nHover;
                        }

                        return Error($"Step '{name}' does not take an argument {argumentName}");
                    }
                }
            }

            var summary = stepFactory.Summary;

            return Description(
                stepFactory.TypeName,
                stepFactory.OutputTypeExplanation,
                summary
            );
        }
        else
        {
            return Error(name);
        }
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitNumber(SCLParser.NumberContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        var text = context.GetText();

        var typeReference = int.TryParse(text, out var _)
            ? TypeReference.Actual.Integer
            : TypeReference.Actual.Double;

        return Description(text, typeReference.Name, null);
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitBoolean(SCLParser.BooleanContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        return Description(
            context.GetText(),
            TypeReference.Actual.Bool.Name,
            null
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitEntity(SCLParser.EntityContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        foreach (var contextChild in context.children)
        {
            var r = Visit(contextChild);

            if (r is not null)
                return r;
        }

        return Description(
            context.GetText(),
            TypeReference.Actual.Entity.Name,
            null
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitDateTime(SCLParser.DateTimeContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        return Description(
            context.GetText(),
            TypeReference.Actual.Date.Name,
            null
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitEnumeration(SCLParser.EnumerationContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        if (context.children.Count != 3 || context.NAME().Length != 2)
            return null;

        var prefix = context.NAME(0).GetText();
        var suffix = context.NAME(1).GetText();

        if (!StepFactoryStore.EnumTypesDictionary.TryGetValue(prefix, out var enumType))
        {
            return Error($"'{prefix}' is not a valid enum type.");
        }

        if (!Enum.TryParse(enumType, suffix, true, out var value))
        {
            return Error($"'{suffix}' is not a member of enumeration '{prefix}'");
        }

        return Description(
            value!.ToString(),
            enumType.Name,
            value.GetType().GetXmlDocsSummary()
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitGetAutomaticVariable(
        SCLParser.GetAutomaticVariableContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        return Description(
            "<>",
            null,
            "Automatic Variable"
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitGetVariable(SCLParser.GetVariableContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        if (LazyTypeResolver.Value.IsFailure)
            return Description(
                context.GetText(),
                nameof(VariableName),
                null
            );

        var vn = new VariableName(context.GetText().TrimStart('<').TrimEnd('>'));

        if (LazyTypeResolver.Value.Value.Dictionary.TryGetValue(vn, out var tr))
        {
            return Description(
                context.GetText(),
                tr.Name,
                null
            );
        }

        return Description(
            context.GetText(),
            nameof(VariableName),
            null
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitSetVariable(SCLParser.SetVariableContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        var variableHover = VisitVariable(context.VARIABLENAME());

        if (variableHover is not null)
            return variableHover;

        var h2 = Visit(context.step());

        if (h2 is not null)
            return h2;

        var setVariable = new SetVariable<int>().StepFactory;

        return Description(
            setVariable.TypeName,
            setVariable.OutputTypeExplanation,
            setVariable.Summary
        );
    }

    private QuickInfoResponse? VisitVariable(ITerminalNode variableNameNode)
    {
        if (!variableNameNode.Symbol.ContainsPosition(LinePosition))
            return null;

        var text = variableNameNode.GetText();

        if (LazyTypeResolver.Value.IsFailure)
            return Description(
                text,
                nameof(VariableName),
                null
            );

        var vn = new VariableName(text.TrimStart('<').TrimEnd('>'));

        if (LazyTypeResolver.Value.Value.Dictionary.TryGetValue(vn, out var tr))
        {
            return Description(
                text,
                tr.Name,
                null
            );
        }

        return Description(
            text,
            nameof(VariableName),
            null
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitQuotedString(SCLParser.QuotedStringContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        return Description(
            context.GetText(),
            TypeReference.Actual.String.Name,
            null
        );
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitArray(SCLParser.ArrayContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        foreach (var contextChild in context.children)
        {
            var h1 = Visit(contextChild);

            if (h1 is not null)
                return h1;
        }

        return DescribeStep(context.GetText());
    }

    /// <inheritdoc />
    public override QuickInfoResponse? VisitInfixOperation(SCLParser.InfixOperationContext context)
    {
        if (!context.ContainsPosition(LinePosition))
            return null;

        foreach (var termContext in context.infixableTerm())
        {
            var h1 = Visit(termContext);

            if (h1 is not null)
                return h1;
        }

        var operatorSymbols =
            context.infixOperator().Select(x => x.GetText()).Distinct().ToList();

        if (operatorSymbols.Count != 1)
        {
            return Error("Invalid mix of operators");
        }

        return DescribeStep(context.GetText());
    }

    private QuickInfoResponse DescribeStep(string text)
    {
        var step = SCLParsing.TryParseStep(text);

        if (step.IsFailure)
            return Error(step.Error.AsString);

        var callerMetadata = new CallerMetadata("Step", "Parameter", TypeReference.Any.Instance);

        Result<IStep, IError> freezeResult;

        if (LazyTypeResolver.Value.IsFailure)
            freezeResult = step.Value.TryFreeze(callerMetadata, StepFactoryStore);
        else
            freezeResult = step.Value.TryFreeze(callerMetadata, LazyTypeResolver.Value.Value);

        if (freezeResult.IsFailure)
            return Error(freezeResult.Error.AsString);

        return Description(freezeResult.Value);
    }

    private static QuickInfoResponse Description(IStep step)
    {
        var     name = step.Name;
        string  type = GetHumanReadableTypeName(step.OutputType);
        string? description;

        if (step is ICompoundStep cs)
        {
            description = cs.StepFactory.Summary;
        }

        else
        {
            description = null;
        }

        return Description(name, type, description);
    }

    private static QuickInfoResponse Description(
        string? name,
        string? type,
        string? summary)
    {
        var markdown = $@"`{name}`
`{type}`
{summary}
";

        return new() { Markdown = markdown };
    }

    private static QuickInfoResponse Error(string message)
    {
        return new() { Markdown = message };
    }

    private static string GetHumanReadableTypeName(Type t)
    {
        if (!t.IsSignatureType && t.IsEnum)
            return t.Name;

        if (TypeAliases.TryGetValue(t, out var name))
            return name;

        if (!t.IsGenericType)
            return t.Name;

        if (t.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(t);

            if (underlyingType == null)
                return t.Name;

            return GetHumanReadableTypeName(underlyingType) + "?";
        }

        var typeName = t.Name.Split("`")[0];

        var arguments =
            $"<{string.Join(",", t.GetGenericArguments().Select(GetHumanReadableTypeName))}>";

        return typeName + arguments;
    }

    private static readonly Dictionary<Type, string> TypeAliases =
        new()
        {
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(object), "object" },
            { typeof(bool), "bool" },
            { typeof(char), "char" },
            { typeof(string), "string" },
            { typeof(StringStream), "string" },
            { typeof(Entity), "entity" },
            { typeof(DateTime), "dateTime" },
            { typeof(void), "void" }
        };
}
