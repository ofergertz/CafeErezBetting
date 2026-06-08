import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/store/authStore'

// Use relative URL so Vite proxy handles routing in dev (same fix as api.ts)
const WS_URL = import.meta.env.VITE_WS_URL ?? ''

export function useMatchesHub(onMatchesUpdated: (matches: unknown[]) => void) {
  const connectionRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${WS_URL}/hubs/matches`)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('MatchesUpdated', onMatchesUpdated)

    connection.start().catch((err) => {
      console.warn('MatchesHub connection failed:', err)
    })

    connectionRef.current = connection

    return () => {
      connection.stop()
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return connectionRef
}

export function useNotificationsHub(handlers: {
  onNewForm?: (data: unknown) => void
  onFormStatusChanged?: (data: unknown) => void
}) {
  const token = useAuthStore((s) => s.token)
  const connectionRef = useRef<signalR.HubConnection | null>(null)

  useEffect(() => {
    if (!token) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${WS_URL}/hubs/notifications`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    if (handlers.onNewForm)
      connection.on('NewForm', handlers.onNewForm)
    if (handlers.onFormStatusChanged)
      connection.on('FormStatusChanged', handlers.onFormStatusChanged)

    connection.start().catch((err) => {
      console.warn('NotificationsHub connection failed:', err)
    })

    connectionRef.current = connection

    return () => { connection.stop() }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  return connectionRef
}
