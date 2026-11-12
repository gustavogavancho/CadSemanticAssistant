using System;
using System.Collections.Generic;
using System.Text;

namespace CadSemanticAssistant.Application.Evaluation;

public enum EvaluationFailureKind
{
    None,
    Validation,
    Input,
    ContextOrConfiguration,
    Unknown
}
