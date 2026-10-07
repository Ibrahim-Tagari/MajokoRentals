// Dr Majoko Projects - Chatbot Widget
// Rule-based chatbot with English and isiZulu support

(function () {
    'use strict';

    // DOM Elements
    const chatToggle = document.getElementById('chatToggle');
    const chatWindow = document.getElementById('chatWindow');
    const chatMessages = document.getElementById('chatMessages');
    const chatInput = document.getElementById('chatInput');
    const chatSend = document.getElementById('chatSend');
    const chatLang = document.getElementById('chatLang');
    const chatMinimize = document.getElementById('chatMinimize');
    const typingIndicator = document.getElementById('typingIndicator');
    const welcomeText = document.getElementById('welcomeText');
    const quickActionBtns = document.querySelectorAll('.quick-action-btn');

    // State
    let isOpen = false;
    let currentLang = localStorage.getItem('chat_lang') || 'en';

    // Initialize
    function init() {
        chatLang.value = currentLang;
        updateWelcomeMessage();
        updatePlaceholder();
        attachEventListeners();

        // Show welcome notification after delay
        setTimeout(() => {
            if (!isOpen && !localStorage.getItem('chat_visited')) {
                showNotification();
            }
        }, 5000);
    }

    // Event Listeners
    function attachEventListeners() {
        chatToggle.addEventListener('click', toggleChat);
        chatMinimize.addEventListener('click', closeChat);
        chatSend.addEventListener('click', sendMessage);
        chatInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });

        chatLang.addEventListener('change', () => {
            currentLang = chatLang.value;
            localStorage.setItem('chat_lang', currentLang);
            updateWelcomeMessage();
            updatePlaceholder();
        });

        // Quick action buttons
        quickActionBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                const message = btn.getAttribute('data-message');
                if (message) {
                    chatInput.value = message;
                    sendMessage();
                }
            });
        });

        // Auto-resize input
        chatInput.addEventListener('input', () => {
            chatSend.disabled = !chatInput.value.trim();
        });
    }

    // Toggle Chat Window
    function toggleChat() {
        isOpen = !isOpen;
        chatToggle.classList.toggle('active');
        chatWindow.classList.toggle('active');

        if (isOpen) {
            chatInput.focus();
            hideNotification();
            localStorage.setItem('chat_visited', 'true');
        }
    }

    // Close Chat Window
    function closeChat() {
        isOpen = false;
        chatToggle.classList.remove('active');
        chatWindow.classList.remove('active');
    }

    // Show Notification Badge
    function showNotification() {
        const badge = document.getElementById('chatNotification');
        if (badge) {
            badge.style.display = 'flex';
        }
    }

    // Hide Notification Badge
    function hideNotification() {
        const badge = document.getElementById('chatNotification');
        if (badge) {
            badge.style.display = 'none';
        }
    }

    // Update Welcome Message
    function updateWelcomeMessage() {
        if (currentLang === 'zu') {
            welcomeText.textContent = "Ngilapha ukusiza ngezindlu zokugasha, izinkokhelo, ukulungisa, nokunye. Ngingakusiza ngani namuhla?";
        } else {
            welcomeText.textContent = "I'm here to help with rentals, payments, maintenance, and more. How can I assist you today?";
        }
    }

    // Update Input Placeholder
    function updatePlaceholder() {
        if (currentLang === 'zu') {
            chatInput.placeholder = "Bhala umlayezo wakho...";
        } else {
            chatInput.placeholder = "Type your message...";
        }
    }

    // Send Message
    async function sendMessage() {
        const text = chatInput.value.trim();
        if (!text) return;

        // Add user message to chat
        addUserMessage(text);

        // Clear input
        chatInput.value = '';
        chatSend.disabled = true;

        // Show typing indicator
        showTyping();

        try {
            const response = await fetch('/Chatbot/SendMessage', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    message: text,
                    lang: currentLang
                })
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();

            // Hide typing indicator
            hideTyping();

            // Add bot response
            addBotMessage(data.reply, data.escalated, data.sessionId, data.navigateTo);

        } catch (error) {
            hideTyping();
            const errorMsg = currentLang === 'zu'
                ? 'Uxolo, kukhona okungalungile. Sicela uzame futhi.'
                : 'Sorry, something went wrong. Please try again.';
            addBotMessage(errorMsg);
            console.error('Chatbot error:', error);
        }
    }

    // Add User Message to Chat
    function addUserMessage(text) {
        const messageDiv = document.createElement('div');
        messageDiv.className = 'chat-message user';

        const avatar = document.createElement('div');
        avatar.className = 'message-avatar';
        avatar.textContent = 'You'[0];

        const bubble = document.createElement('div');
        bubble.className = 'message-bubble';
        bubble.textContent = text;

        messageDiv.appendChild(avatar);
        messageDiv.appendChild(bubble);

        // Remove welcome message if present
        const welcomeMsg = chatMessages.querySelector('.welcome-message');
        if (welcomeMsg) {
            welcomeMsg.remove();
        }

        chatMessages.appendChild(messageDiv);
        scrollToBottom();
    }

    // Add Bot Message to Chat
    function addBotMessage(text, escalated = false, sessionId = null, navigateTo = null) {
        const messageDiv = document.createElement('div');
        messageDiv.className = 'chat-message bot';

        const avatar = document.createElement('div');
        avatar.className = 'bot-message-avatar';
        avatar.innerHTML = `<svg viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z"/>
        </svg>`;

        const bubble = document.createElement('div');
        bubble.className = 'message-bubble';
        bubble.innerHTML = formatMessage(text);

        // Add navigation to chat if escalated
        if (escalated && sessionId) {
            const statusMsg = document.createElement('p');
            statusMsg.style.cssText = 'margin-top: 10px; padding: 8px; background: rgba(186, 170, 73, 0.1); border-left: 3px solid var(--accent-gold); font-size: 0.9em;';
            statusMsg.innerHTML = currentLang === 'zu'
                ? '<strong>✓ Isicelo senziwe!</strong><br>Umphathi uzobona isicelo sakho futhi uzokuxhumana maduze.'
                : '<strong>✓ Request sent!</strong><br>An admin will see your request and respond shortly.';
            bubble.appendChild(statusMsg);

            const navBtn = document.createElement('a');
            navBtn.className = 'navigation-button escalation-button';
            navBtn.href = `/Messaging/MyChats`;
            navBtn.textContent = currentLang === 'zu'
                ? '➜ Buka Izingxoxo Zami'
                : '➜ View My Chats';
            navBtn.style.cssText = 'display: inline-block; margin-top: 10px; padding: 8px 16px; background: var(--accent-gold); color: var(--primary-bg); border-radius: 5px; text-decoration: none; font-weight: bold;';
            bubble.appendChild(navBtn);
        }

        // Add navigation button if navigateTo is provided
        if (navigateTo) {
            const navBtn = document.createElement('a');
            navBtn.className = 'navigation-button';
            navBtn.href = navigateTo;
            navBtn.textContent = currentLang === 'zu' ? '➜ Yiya Ekhoneni' : '➜ Go There';
            bubble.appendChild(document.createElement('br'));
            bubble.appendChild(navBtn);
        }

        messageDiv.appendChild(avatar);
        messageDiv.appendChild(bubble);
        chatMessages.appendChild(messageDiv);
        scrollToBottom();
    }

    // Format Message (preserve line breaks, convert links)
    function formatMessage(text) {
        // Convert line breaks to <br>
        text = text.replace(/\n/g, '<br>');

        // Convert bullet points
        text = text.replace(/•/g, '<span style="color: var(--chat-accent);">•</span>');

        // Convert URLs to links
        const urlRegex = /(https?:\/\/[^\s]+)/g;
        text = text.replace(urlRegex, '<a href="$1" target="_blank" style="color: var(--chat-accent); text-decoration: underline;">$1</a>');

        return text;
    }

    // Show Typing Indicator
    function showTyping() {
        typingIndicator.style.display = 'flex';
        scrollToBottom();
    }

    // Hide Typing Indicator
    function hideTyping() {
        typingIndicator.style.display = 'none';
    }

    // Scroll to Bottom of Chat
    function scrollToBottom() {
        setTimeout(() => {
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }, 100);
    }

    // Escape HTML
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
