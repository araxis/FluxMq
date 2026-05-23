namespace FluxMq.Pipeline.Mapping;

public interface IFlowPredicate<in TInput>
{
    bool IsMatch(TInput input);
}
