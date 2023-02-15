/*
 * Solfar - Solfar Skylounge Automation
 * Copyright (C) 2020-2023 - Steve G. Bjorg
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
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RadiantPi.Cortex {

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
        private List<(string Name, Func<object, Task> Action, object State)> _triggered = new();

        //--- Constructors ---
        protected AController(ILogger? logger = null) => Logger = logger;

        //--- Properties ---
        protected ILogger? Logger { get; }

        //--- Abstract Methods ---
        protected abstract Task ProcessEventAsync(object? sender, EventArgs args, CancellationToken cancellationToken);

        //--- Methods ---
        public virtual async Task Run(CancellationToken cancellationToken = default) {

            // kick-off channel received as a background thread
            var runner = Task.Run(() => ChannelReceiverAsync(cancellationToken));

            // initialize controller
            await Initialize(cancellationToken).ConfigureAwait(false);

            // wait until the event channel is closed
            await runner.ConfigureAwait(false);

            // shutdown the controller
            await Shutdown(cancellationToken).ConfigureAwait(false);
        }

        public virtual void Close() => _channel.Writer.Complete();

        protected virtual void EventListener(object? sender, EventArgs args) {
            Logger?.LogTrace($"{nameof(EventListener)}: (Sender='{sender?.GetType().FullName ?? "<null>"}', Args='{args?.GetType().FullName ?? "<null>"}')");
            _channel.Writer.TryWrite((Sender: sender, EventArgs: args!));
        }

        protected virtual Task Initialize(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task Shutdown(CancellationToken cancellationToken) => Task.CompletedTask;

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
            object? oldState = null;
            try {
                if(_rules.TryGetValue(name, out oldState)) {

                    // check if the state change triggered an action
                    if(condition((T)oldState, newState)) {
                        Logger?.LogDebug($"Trigger rule '{name}': {oldState} --> {newState}");
                        _triggered.Add((Name: name, Action: state => callback((T)state), State: (object)newState));
                    } else {
                        Logger?.LogDebug($"Ignore rule '{name}': {oldState} --> {newState}");
                    }
                } else {
                    Logger?.LogDebug($"Set rule '{name}' state for the first time (state: {newState})");
                }

                // record current condition state
                _rules[name] = newState;
            } catch(Exception e) {
                Logger?.LogError(e, $"Exception while evaluating rule condition '{name}' (new state: {newState}, old state: {oldState ?? "<null>"})");
            }
        }

        private async Task ChannelReceiverAsync(CancellationToken cancellationToken) {

            // process all changes in the channel
            await foreach(var change in _channel.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken)) {
                _triggered.Clear();

                // evaluate event
                try {
                    Logger?.LogDebug($"Evaluating event");
                    await ProcessEventAsync(change.Sender, change.EventArgs, cancellationToken).ConfigureAwait(false);
                    Logger?.LogDebug($"Triggered {_triggered.Count:N0} rules to execute");
                } catch(Exception e) {
                    Logger?.LogError(e, "Exception while processing event");
                }

                // execute triggered rules
                try {
                    foreach(var triggered in _triggered) {
                        cancellationToken.ThrowIfCancellationRequested();
                        Logger?.LogInformation($"Executing rule '{triggered.Name}'");
                        try {

                            // triggered actions are expected to be short; therefore, they don't take a cancellation token
                            await triggered.Action(triggered.State).ConfigureAwait(false);
                        } catch(Exception e) {
                            Logger?.LogError(e, $"Exception while executing rule '{triggered.Name}'");
                        }
                    }
                } catch(Exception e) {
                    Logger?.LogError(e, "Exception while executing rules");
                }
            }
        }
    }
}
