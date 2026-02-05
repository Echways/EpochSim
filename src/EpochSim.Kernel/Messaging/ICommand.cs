namespace EpochSim.Kernel.Messaging;

public interface ICommand
{
    string Kind => MessageKinds.GetKind(GetType());
}
