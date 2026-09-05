import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CopilotChatComponent } from './copilot-chat.component';

describe('CopilotChatComponent', () => {
  let component: CopilotChatComponent;
  let fixture: ComponentFixture<CopilotChatComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CopilotChatComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CopilotChatComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and initialize with empty input', () => {
    expect(component).toBeTruthy();
    expect(component.inputText).toBe('');
    expect(component.messages.length).toBe(0);
  });

  it('should not emit message if input is whitespace only', () => {
    spyOn(component.sendMessage, 'emit');
    component.inputText = '    ';
    component.onSend();
    expect(component.sendMessage.emit).not.toHaveBeenCalled();
  });

  it('should emit message and clear input on valid onSend', () => {
    spyOn(component.sendMessage, 'emit');
    component.inputText = 'How can our squad reduce cycle time?';
    component.onSend();
    expect(component.sendMessage.emit).toHaveBeenCalledWith('How can our squad reduce cycle time?');
    expect(component.inputText).toBe('');
  });
});
