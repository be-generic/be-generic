using BeGeneric.Backend.Common;

namespace BeGeneric.Backend.Services.GenericBackend;

public class AttachedActionService<T> : IAttachedActionService<T>
{
    private Dictionary<string, Dictionary<ActionType, Dictionary<ActionOrderType, Func<ActionData<T>, Task>>>> attachedActions = new Dictionary<string, Dictionary<ActionType, Dictionary<ActionOrderType, Func<ActionData<T>, Task>>>>();

    public Func<ActionData<G, T>, Task> GetAttachedAction<G>(string controllerName, ActionType actionType, ActionOrderType actionOrder)
    {
        return attachedActions.ContainsKey(controllerName) && attachedActions[controllerName].ContainsKey(actionType) && attachedActions[controllerName][actionType].ContainsKey(actionOrder)
            ? attachedActions[controllerName][actionType][actionOrder]
            : (Func<ActionData<G, T>, Task>?)null;
    }

    public Func<ActionData<T>, Task> GetAttachedAction(string controllerName, ActionType actionType, ActionOrderType actionOrder)
    {
        return attachedActions.ContainsKey(controllerName) && attachedActions[controllerName].ContainsKey(actionType) && attachedActions[controllerName][actionType].ContainsKey(actionOrder)
            ? attachedActions[controllerName][actionType][actionOrder]
            : null;
    }

    public void RegisterAttachedAction<G>(string controllerName, ActionType actionType, ActionOrderType actionOrder, Func<ActionData<G, T>, Task> action)
    {
        if (!attachedActions.ContainsKey(controllerName))
        {
            attachedActions[controllerName] = new Dictionary<ActionType, Dictionary<ActionOrderType, Func<ActionData<T>, Task>>>();
        }

        foreach (ActionType actionValue in Enum.GetValues(typeof(ActionType)))
        {
            if (actionType.HasFlag(actionValue))
            {
                if (!attachedActions[controllerName].ContainsKey(actionValue))
                {
                    attachedActions[controllerName][actionValue] = new Dictionary<ActionOrderType, Func<ActionData<T>, Task>>();
                }

                attachedActions[controllerName][actionValue][actionOrder] = async (x) => await action.Invoke(new ActionData<G, T>(x));
            }
        }
    }
}
