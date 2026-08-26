import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';

@Component({
  selector: 'app-copilot-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './copilot-chat.component.html',
  styleUrl: './copilot-chat.component.css'
})
export class CopilotChatComponent {
  @Input() messages: { isUser: boolean; text: string }[] = [];
  @Output() sendMessage = new EventEmitter<string>();

  inputText = '';

  onSend() {
    if (!this.inputText.trim()) return;
    this.sendMessage.emit(this.inputText);
    this.inputText = '';
  }
}
