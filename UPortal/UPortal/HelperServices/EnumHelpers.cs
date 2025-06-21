using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using UPortal.Data.Models; // For SeniorityLevelEnum

namespace UPortal.HelperServices
{
    /// <summary>
    /// Represents an item in a list, typically for dropdowns, pairing a value with a display name.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class SelectListItem<TValue>
    {
        /// <summary>
        /// Gets or sets the display text for the item.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value for the item.
        /// </summary>
        public TValue Value { get; set; } = default!;
    }

    /// <summary>
    /// Provides helper methods for working with enums, particularly for UI display.
    /// </summary>
    public static class EnumHelpers
    {
        /// <summary>
        /// Gets the display name for an enum value.
        /// Uses the <see cref="DisplayAttribute"/> if present, otherwise returns the enum member's string representation.
        /// </summary>
        /// <typeparam name="TEnum">The type of the enum.</typeparam>
        /// <param name="enumValue">The enum value.</param>
        /// <returns>The display name.</returns>
        public static string GetDisplayName<TEnum>(TEnum enumValue) where TEnum : struct, Enum
        {
            return enumValue.GetType()
                            .GetMember(enumValue.ToString())
                            .First()
                            .GetCustomAttribute<DisplayAttribute>()?
                            .GetName() ?? enumValue.ToString();
        }

        /// <summary>
        /// Retrieves a list of <see cref="SelectListItem{TValue}"/> for a given enum type,
        /// using the enum members' names or <see cref="DisplayAttribute"/> for display text.
        /// </summary>
        /// <typeparam name="TEnum">The enum type to get values from.</typeparam>
        /// <returns>A list of select list items.</returns>
        public static List<SelectListItem<TEnum>> GetSelectListItems<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetValues(typeof(TEnum))
                       .Cast<TEnum>()
                       .Select(e => new SelectListItem<TEnum>
                       {
                           Value = e,
                           DisplayName = GetDisplayName(e)
                       })
                       .ToList();
        }

        /// <summary>
        /// Retrieves a list of <see cref="SelectListItem{TValue}"/> for <see cref="SeniorityLevelEnum"/>.
        /// This can be used to populate dropdowns for selecting a seniority level.
        /// Includes an option for "Not Set" or "None".
        /// </summary>
        /// <param name="includeNoneOption">If true, adds a "None" option with a null value at the beginning.</param>
        /// <param name="noneOptionText">The text for the "None" option if included.</param>
        /// <returns>A list of select list items for SeniorityLevelEnum.</returns>
        public static List<SelectListItem<SeniorityLevelEnum?>> GetSeniorityLevelSelectListItems(bool includeNoneOption = false, string noneOptionText = "-- Not Set --")
        {
            var items = Enum.GetValues(typeof(SeniorityLevelEnum))
                            .Cast<SeniorityLevelEnum>()
                            .Select(e => new SelectListItem<SeniorityLevelEnum?>
                            {
                                Value = e,
                                DisplayName = GetDisplayName(e)
                            })
                            .ToList();

            if (includeNoneOption)
            {
                items.Insert(0, new SelectListItem<SeniorityLevelEnum?> { Value = null, DisplayName = noneOptionText });
            }
            return items;
        }
    }
}
