using Augram.Core.Steps;

namespace Augram.Core.Mapping;

/// <summary>One step of a command as it runs on a platform (<see cref="Command.PlanFor"/>): the stored step (its active flag, where it was authored) and what runs (<see cref="StepConversion"/>).</summary>
public readonly record struct PlannedStep(CommandStep Stored, StepConversion Run);
