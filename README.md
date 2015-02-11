# myedit

This is a **proof of concept** to see how hard it would be to write a code editor that

* Is native (WPF), no html/javascript: I really like the idea behind Brackets/Atom but here I want things blazing fast
* Embeded terminal to be able to run command within the editor
* Embeded file watcher that could trigger custom command line scripts
* Multi-rows of tabs, I love tabs, I open a lot of them, I don't like to scroll tabs !

#Why
Why am I writing my own code editor ?

* It's a fun exercise
* I like to learn different programming lanugages but I have not yet found lightweight windows code editor
* Lightweight to me mean: fast to start, reactive UI, bare minimun features
* I want multi row tabs
* I want an embeded command shell
* I want to be able to trigger script on file change and set that up EASILY

# History
## Version 1

### Design 

* Really poor but we start to get a feel of multirows tabs
* Tree File browser control in place but not coded up
* File menu in place but not wired up

### Code editor
* This is avalon edit, it just works
  
### Terminal
* It's possible to send command and it displays the output !
* It's slow !

![shot](https://github.com/lepinay/myedit/blob/master/Versions/1.png) 






