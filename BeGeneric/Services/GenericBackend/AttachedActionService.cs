using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BeGeneric.Services.BeGeneric
{
    public class AttachedActionService: IAttachedActionService
    {
        private Dictionary<string, Dictionary<ActionType, Dictionary<ActionOrderType, Func<ActionData, Task>>>> attachedActions = new Dictionary<string, Dictionary<ActionType, Dictionary<ActionOrderType, Func<ActionData, Task>>>>();

        public Func<ActionData<T>, Task> GetAttachedAction<T>(string controllerName, ActionType actionType, ActionOrderType actionOrder)
        {
            if (attachedActions.ContainsKey(controllerName) && attachedActions[controllerName].ContainsKey(actionType) && attachedActions[controllerName][actionType].ContainsKey(actionOrder))
            {
                return attachedActions[controllerName][actionType][actionOrder];
            }

            return null;
        }

        public Func<ActionData, Task> GetAttachedAction(string controllerName, ActionType actionType, ActionOrderType actionOrder)
        {
            if (attachedActions.ContainsKey(controllerName) && attachedActions[controllerName].ContainsKey(actionType) && attachedActions[controllerName][actionType].ContainsKey(actionOrder))
            {
                return attachedActions[controllerName][actionType][actionOrder];
            }

            return null;
        }

        public void RegisterAttachedAction<T>(string controllerName, ActionType actionType, ActionOrderType actionOrder, Func<ActionData<T>, Task> action)
        {
            if (!attachedActions.ContainsKey(controllerName))
            {
                attachedActions[controllerName] = new Dictionary<ActionType, Dictionary<ActionOrderType, Func<ActionData, Task>>>();
            }

            foreach (ActionType actionValue in Enum.GetValues(typeof(ActionType)))
            {
                if (actionType.HasFlag(actionValue))
                {
                    if (!attachedActions[controllerName].ContainsKey(actionValue))
                    {
                        attachedActions[controllerName][actionValue] = new Dictionary<ActionOrderType, Func<ActionData, Task>>();
                    }

                    attachedActions[controllerName][actionValue][actionOrder] = async (x) => await action.Invoke(new ActionData<T>(x));
                }
            }
        }
    }
}
