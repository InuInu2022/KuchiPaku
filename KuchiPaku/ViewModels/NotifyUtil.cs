using System;
using Enterwell.Clients.Wpf.Notifications;

namespace KuchiPaku.ViewModels;
public static class NotifyUtil
{
	/// <summary>
	/// 警告メッセージ通知（2行）
	/// </summary>
	/// <param name="manager"></param>
	/// <param name="header">ヘッダーテキスト</param>
	/// <param name="message">詳細メッセージ</param>
	/// <returns></returns>
	public static INotificationMessageManager Warn(
		this INotificationMessageManager manager,
		string header,
		string message
	)
	{
		manager
			.CreateMessage()
			.Accent("#E0A030")
			.Background("#333")
			.HasHeader(header)
			.HasBadge("Warn")
			.HasMessage(message)
			.Dismiss()
			.WithButton("OK", _ => { })
			.Animates(true)
			.AnimationInDuration(0.35)
			.Queue();
		return manager;
	}
}
