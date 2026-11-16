export type AccountProfile = {
    id: string;
    username: string;
    displayName: string;
}

export type ChatSummary = {
    id: string;
    peer: AccountProfile;
    createdAt: string;
    lastMessageAt: string | null;
}

export type ChatMessage = {
    id: string;
    chatId: string;
    senderId: string;
    clientMessageId: string;
    sequence: number;
    text: string;
    createdAt: string;
}

export type HistoryResponse = {
    items: ChatMessage[];
    nexSeq: number | null;
}

export type ChatsResponse = { items: ChatSummary[] };