using UnityEngine;

namespace LTSystem
{
    [CreateAssetMenu(menuName = "Eroticissima/LoveTriggerDatabase")]
    public class LoveTriggerDatabase : ScriptableObject
    {
        [Header("Trigger Collection")]
        public LoveTriggerSO[] triggers;

        // Runtime lookup dictionary
        private System.Collections.Generic.Dictionary<string, LoveTriggerSO> triggerLookup;

        public void Initialize()
        {
            triggerLookup = new System.Collections.Generic.Dictionary<string, LoveTriggerSO>();

            if (triggers == null) return;

            foreach (var trigger in triggers)
            {
                if (trigger != null && !string.IsNullOrEmpty(trigger.triggerID))
                {
                    if (triggerLookup.ContainsKey(trigger.triggerID))
                    {
                        Debug.LogWarning($"[LoveTriggerDatabase] Duplicate trigger ID found: {trigger.triggerID}");
                        continue;
                    }

                    triggerLookup[trigger.triggerID] = trigger;
                }
            }

            Debug.Log($"[LoveTriggerDatabase] Initialized with {triggerLookup.Count} triggers");
        }

        public LoveTriggerSO Get(string triggerID)
        {
            if (triggerLookup == null) Initialize();

            if (string.IsNullOrEmpty(triggerID)) return null;

            return triggerLookup.ContainsKey(triggerID) ? triggerLookup[triggerID] : null;
        }

        public LoveTriggerSO[] GetByCategory(string category)
        {
            if (triggers == null) return new LoveTriggerSO[0];

            var categoryTriggers = new System.Collections.Generic.List<LoveTriggerSO>();

            foreach (var trigger in triggers)
            {
                if (trigger != null && trigger.category == category)
                {
                    categoryTriggers.Add(trigger);
                }
            }

            return categoryTriggers.ToArray();
        }

        public string[] GetAllCategories()
        {
            if (triggers == null) return new string[0];

            var categories = new System.Collections.Generic.HashSet<string>();

            foreach (var trigger in triggers)
            {
                if (trigger != null && !string.IsNullOrEmpty(trigger.category))
                {
                    categories.Add(trigger.category);
                }
            }

            var result = new string[categories.Count];
            categories.CopyTo(result);
            return result;
        }

        // Validation and debug
        [ContextMenu("Validate Database")]
        public void ValidateDatabase()
        {
            Debug.Log("=== LoveTriggerDatabase Validation ===");

            if (triggers == null)
            {
                Debug.LogError("Triggers array is null!");
                return;
            }

            int validTriggers = 0;
            int invalidTriggers = 0;
            var duplicateIDs = new System.Collections.Generic.HashSet<string>();
            var foundIDs = new System.Collections.Generic.HashSet<string>();

            foreach (var trigger in triggers)
            {
                if (trigger == null)
                {
                    invalidTriggers++;
                    Debug.LogWarning("Found null trigger in database");
                    continue;
                }

                if (string.IsNullOrEmpty(trigger.triggerID))
                {
                    invalidTriggers++;
                    Debug.LogWarning($"Trigger '{trigger.triggerName}' has empty ID");
                    continue;
                }

                if (foundIDs.Contains(trigger.triggerID))
                {
                    duplicateIDs.Add(trigger.triggerID);
                    Debug.LogWarning($"Duplicate trigger ID: {trigger.triggerID}");
                }
                else
                {
                    foundIDs.Add(trigger.triggerID);
                }

                validTriggers++;
            }

            Debug.Log($"Valid Triggers: {validTriggers}");
            Debug.Log($"Invalid Triggers: {invalidTriggers}");
            Debug.Log($"Duplicate IDs: {duplicateIDs.Count}");
            Debug.Log($"Categories: {GetAllCategories().Length}");
            Debug.Log("=====================================");
        }
    }
}