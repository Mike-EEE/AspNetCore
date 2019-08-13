// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.RazorComponents;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Mvc.Rendering
{
    /// <summary>
    /// Extensions for rendering components.
    /// </summary>
    public static class HtmlHelperComponentExtensions
    {
        /// <summary>
        /// Renders the <typeparamref name="TComponent"/> <see cref="IComponent"/>.
        /// </summary>
        /// <param name="htmlHelper">The <see cref="IHtmlHelper"/>.</param>
        /// <param name="renderMode">The <see cref="RenderMode"/> for the component.</param>
        /// <returns>The HTML produced by the rendered <typeparamref name="TComponent"/>.</returns>
        public static Task<IHtmlContent> RenderComponentAsync<TComponent>(this IHtmlHelper htmlHelper, RenderMode renderMode) where TComponent : IComponent
        {
            if (htmlHelper == null)
            {
                throw new ArgumentNullException(nameof(htmlHelper));
            }

            return htmlHelper.RenderComponentAsync<TComponent>(null, renderMode);
        }

        /// <summary>
        /// Renders the <typeparamref name="TComponent"/> <see cref="IComponent"/>.
        /// </summary>
        /// <param name="htmlHelper">The <see cref="IHtmlHelper"/>.</param>
        /// <param name="parameters">An <see cref="object"/> containing the parameters to pass
        /// to the component.</param>
        /// <param name="renderMode">The <see cref="RenderMode"/> for the component.</param>
        /// <returns>The HTML produced by the rendered <typeparamref name="TComponent"/>.</returns>
        public static async Task<IHtmlContent> RenderComponentAsync<TComponent>(
            this IHtmlHelper htmlHelper,
            object parameters,
            RenderMode renderMode) where TComponent : IComponent
        {
            if (htmlHelper == null)
            {
                throw new ArgumentNullException(nameof(htmlHelper));
            }

            if (renderMode == default)
            {
                throw new ArgumentException("Can't render a component statically without prerendering it.", nameof(renderMode));
            }

            var parametersCollection = parameters == null ?
                ParameterView.Empty :
                ParameterView.FromDictionary(HtmlHelper.ObjectToDictionary(parameters));

            var context = htmlHelper.ViewContext.HttpContext;
            switch (renderMode)
            {
                case RenderMode.Server:
                    return NonPrerenderedBlazorComponent(context, typeof(TComponent), parametersCollection);
                case RenderMode.ServerPrerendered:
                    return await PrerenderedBlazorComponentAsync(context, typeof(TComponent), parametersCollection);
                case RenderMode.Html:
                    return await StaticComponentAsync(context, typeof(TComponent), parametersCollection);
                default:
                    throw new ArgumentException("Invalid render mode", nameof(renderMode));
            }
        }

        private static async Task<IHtmlContent> StaticComponentAsync(HttpContext context, Type type, ParameterView parametersCollection)
        {
            var serviceProvider = context.RequestServices;
            var prerenderer = serviceProvider.GetRequiredService<StaticComponentRenderer>();


            var result = await prerenderer.PrerenderComponentAsync(
                parametersCollection,
                context,
                type);

            return new ComponentHtmlContent(result);
        }

        private static async Task<IHtmlContent> PrerenderedBlazorComponentAsync(HttpContext context, Type type, ParameterView parametersCollection)
        {
            var serviceProvider = context.RequestServices;
            var prerenderer = serviceProvider.GetRequiredService<StaticComponentRenderer>();
            var invocationSerializer = serviceProvider.GetRequiredService<ComponentDescriptorSerializer>();

            var currentInvocation = invocationSerializer.SerializeInvocation(
                context,
                type,
                parametersCollection,
                prerendered: true);

            var result = await prerenderer.PrerenderComponentAsync(
                parametersCollection,
                context,
                type);

            return new ComponentHtmlContent(
                invocationSerializer.GetPreamble(currentInvocation),
                result,
                invocationSerializer.GetEpilogue(currentInvocation));
        }

        private static IHtmlContent NonPrerenderedBlazorComponent(HttpContext context, Type type, ParameterView parametersCollection)
        {
            var serviceProvider = context.RequestServices;
            var invocationSerializer = serviceProvider.GetRequiredService<ComponentDescriptorSerializer>();
            var currentInvocation = invocationSerializer.SerializeInvocation(context, type, parametersCollection, prerendered:false);

            return new ComponentHtmlContent(invocationSerializer.GetPreamble(currentInvocation));
        }
    }
}
