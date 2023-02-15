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

using System.Threading.Tasks;

namespace RadiantPi.Lumagen {

    public static class IRadianceProEx {

        //--- Class Methods ---
        public static Task ShowMessageCenteredAsync(this IRadiancePro client, string message, int delay)
            => ShowMessageCenteredAsync(client, message, "", delay);

        public static Task ShowMessageCenteredAsync(this IRadiancePro client, string messageLine1, string messageLine2, int delay)
            => client.ShowMessageAsync(Center(messageLine1) + Center(messageLine2), delay);

        private static string Center(string text, int length = 30) {
            if(text.Length == 0) {

                // nothing to do with an empty text
                return "";
            }
            if(text.Length > length) {

                // truncate when text is too long
                return text.Substring(0, length);
            }

            // NOTE (2021-12-09, bjorg): taken from StackOverflow: https://stackoverflow.com/a/17590723
            var padding = length - text.Length;
            var padLeft = (padding / 2) + text.Length;
            return text.PadLeft(padLeft).PadRight(length);
        }
    }
}
