/*
 * Solfar - Solfar Skylounge Automation
 * Copyright (C) 2020-2021 - Steve G. Bjorg
 *
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for more
 * details.
 *
 * You should have received a copy of the GNU Affero General Public License along
 * with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RadiantPi.Controller {

    public abstract class AController {

        //--- Class Methods ---
        protected static bool LessThan(string left, string right) => StringComparer.Ordinal.Compare(left, right) < 0;
        protected static bool LessThanOrEqual(string left, string right) => StringComparer.Ordinal.Compare(left, right) <= 0;
        protected static bool Equal(string left, string right) => StringComparer.Ordinal.Compare(left, right) == 0;
        protected static bool GreaterThanOrEqual(string left, string right) => StringComparer.Ordinal.Compare(left, right) >= 0;
        protected static bool GreaterThan(string left, string right) => StringComparer.Ordinal.Compare(left, right) > 0;

        //--- Fields ---
        private Dictionary<string, object> _rules = new();
        private Channel<(object? Sender, EventArgs EventArgs)> _channel = Channel.CreateUnbounded<(object? Sender, EventArgs EventArgs)>();
        private TaskCompletionSource _taskCompletionSource = new();
        private List<(string Name, Func<object, Task> Action, object State)> _triggered = new();

        //--- Constructors ---
        protected AController(ILogger? logger = null) => Logger = logger;

        //--- Properties ---
        protected ILogger? Logger { get; }

        //--- Abstract Methods ---
        protected abstract bool ApplyEvent(object? sender, EventArgs change);
        protected abstract void Evaluate();

        //--- Methods ---
        public virtual void EventListener(object? sender, EventArgs args) => _channel.Writer.TryWrite((Sender: sender, EventArgs: args));

        public virtual Task Start() {
            Task.Run((Func<Task>)(async () => {

                // process all changes in the channel
                await foreach(var change in _channel.Reader.ReadAllAsync()) {
                    if(ApplyEvent(change.Sender, change.EventArgs)) {
                        await EvaluateChangeAsync().ConfigureAwait(false);
                    }
                }

                // signal the orchestrator is done
                _taskCompletionSource.SetResult();
            }));
            return Task.CompletedTask;
        }

        public virtual void Stop() => _channel.Writer.Complete();
        public virtual Task WaitAsync() => _taskCompletionSource.Task;

        protected virtual async Task EvaluateChangeAsync() {
            _triggered.Clear();
            Logger?.LogDebug($"Evaluating changes");
            Evaluate();
            Logger?.LogDebug($"Triggered {_triggered.Count:N0} rules to execute");
            foreach(var triggered in _triggered) {
                Logger?.LogInformation($"Executing rule '{triggered.Name}'");
                try {
                    await triggered.Action(triggered.State).ConfigureAwait(false);
                } catch(Exception e) {
                    Logger?.LogError(e, $"Exception while executing rule '{triggered.Name}'");
                }
            }
        }

        protected virtual void OnTrue(string name, bool condition, Func<Task> callback)
            => OnCondition(name, condition, (oldState, newState) => newState && !oldState, _ => callback());

        protected virtual void OnValueChanged<T>(string name, T value, Func<T, Task> callback) where T : notnull
            => OnCondition(name, value, (oldState, newState) => !object.Equals(oldState, newState), callback);

        protected virtual void OnCondition<T>(string name, T newState, Func<T, T, bool> condition, Func<T, Task> callback) where T : notnull {
            if(condition is null) {
                throw new ArgumentNullException(nameof(condition));
            }
            if(callback is null) {
                throw new ArgumentNullException(nameof(callback));
            }

            // check if this rule is being seen for the first time
            if(_rules.TryGetValue(name, out var oldState)) {
                try {
                    if(condition((T)oldState, newState)) {
                        Logger?.LogDebug($"Trigger rule '{name}': {oldState} --> {newState}");
                        _triggered.Add((Name: name, Action: state => callback((T)state), State: (object)newState));
                    } else {
                        Logger?.LogDebug($"Ignore rule '{name}': {oldState} --> {newState}");
                    }
                } catch(Exception e) {
                    Logger?.LogError(e, $"Exception while evaluating rule condition '{name}' (new state: {newState}, old state: {oldState})");
                }
            } else {
                Logger?.LogDebug($"Set rule '{name}' state for the first time (state: {newState})");
            }

            // record current condition state
            _rules[name] = newState;
        }
    }
}
